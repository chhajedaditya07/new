-- Mahavira Fashion — Complaint Management System
-- DRAFT schema for review (PostgreSQL 15 / Supabase). Not applied anywhere yet.
-- Conventions: uuid PKs, timestamptz everywhere (shown in IST by the app),
-- money as numeric(12,2) in INR, mobile numbers stored as E.164 (+91XXXXXXXXXX),
-- soft delete via deleted_at (no DELETE grants on business tables).

create extension if not exists pgcrypto;
create extension if not exists pg_trgm;

-- ───────────────────────── Enums ─────────────────────────
create type user_role         as enum ('owner', 'staff');
create type complaint_status  as enum ('new','acknowledged','in_progress','waiting_customer',
                                       'waiting_supplier_courier','resolved','closed','reopened','rejected');
create type priority_level    as enum ('low','medium','high','urgent');
create type complaint_source  as enum ('walk_in','phone','whatsapp','website','instagram','email');
create type purchase_channel  as enum ('store','online');
create type payment_mode      as enum ('prepaid','cod','cash','upi','card','other');
create type resolution_type   as enum ('replacement','exchange','repair_alteration','refund',
                                       'store_credit','explanation_only','rejected');
create type approval_status   as enum ('not_required','pending','approved','denied');
create type lang_code         as enum ('en','hi');
create type actor_kind        as enum ('staff','customer','system');
create type event_visibility  as enum ('internal','customer');
create type notify_channel    as enum ('whatsapp','sms','email');
create type notify_status     as enum ('queued','sent','delivered','failed','skipped');
create type event_type        as enum (
  'created','edited','status_changed','assigned','priority_changed','category_changed',
  'note_internal','update_customer','customer_reply','attachment_added',
  'resolution_set','refund_requested','refund_approved','refund_denied',
  'sla_ack_breached','sla_resolve_breached','reminder_sent',
  'notification_sent','notification_failed','reopened','auto_closed',
  'feedback_received','soft_deleted','restored');

-- ───────────────────────── Settings ─────────────────────────
-- key/value JSON: 'store' {name, brand, address, phone, email, gstin, logo_path, ticket_prefix},
--                 'general' {auto_close_days:7, public_form_enabled:true, refund_approval_threshold:0, ...}
create table settings (
  key        text primary key,
  value      jsonb not null,
  updated_at timestamptz not null default now(),
  updated_by uuid
);

create table business_hours (
  weekday   smallint primary key check (weekday between 0 and 6),   -- 0 = Sunday
  opens     time,
  closes    time,
  is_closed boolean not null default false
);
create table holidays (
  day   date primary key,
  label text not null
);

-- ───────────────────────── Users ─────────────────────────
create table profiles (                      -- 1:1 with Supabase auth.users
  id                uuid primary key references auth.users(id),
  full_name         text not null,
  mobile            text check (mobile ~ '^\+91[6-9][0-9]{9}$'),
  role              user_role not null default 'staff',
  can_view_reports  boolean not null default false,     -- per-staff toggle; owner always true
  preferred_lang    lang_code not null default 'en',
  is_active         boolean not null default true,
  created_at        timestamptz not null default now(),
  deleted_at        timestamptz
);

-- ───────────────────────── Master data ─────────────────────────
create table categories (                    -- admin-editable; parent_id gives sub-categories
  id               uuid primary key default gen_random_uuid(),
  parent_id        uuid references categories(id),
  name_en          text not null,
  name_hi          text not null,
  default_priority priority_level not null default 'medium',
  sort_order       int not null default 0,
  is_active        boolean not null default true,
  deleted_at       timestamptz
);

create table suppliers (
  id         uuid primary key default gen_random_uuid(),
  name       text not null,
  contact    text,
  city       text,
  notes      text,
  is_active  boolean not null default true,
  deleted_at timestamptz
);

create table products (                      -- light catalogue; filled from Shopify or manually
  id                 uuid primary key default gen_random_uuid(),
  shopify_product_id bigint unique,
  name               text not null,
  sku                text,
  product_type       text,                   -- saree / suit / co-ord / lehenga
  supplier_id        uuid references suppliers(id),
  deleted_at         timestamptz
);
create index on products (sku);

-- ───────────────────────── Customers & orders ─────────────────────────
create table customers (
  id         uuid primary key default gen_random_uuid(),
  name       text not null,
  mobile     text not null unique check (mobile ~ '^\+91[6-9][0-9]{9}$'),
  email      text,
  city       text,
  notes      text,
  created_at timestamptz not null default now(),
  deleted_at timestamptz
);
create index customers_name_trgm on customers using gin (name gin_trgm_ops);

create table orders (                        -- cache of Shopify orders + manual store bills
  id                uuid primary key default gen_random_uuid(),
  channel           purchase_channel not null,
  shopify_order_id  bigint unique,
  order_no          text,                    -- Shopify '#1042'
  store_bill_no     text,                    -- store channel, typed by staff
  customer_id       uuid references customers(id),
  total_amount      numeric(12,2),
  payment_mode      payment_mode,
  payment_gateway   text,                    -- e.g. 'razorpay', 'cash_on_delivery'
  financial_status  text,
  fulfillment_status text,
  courier           text,
  tracking_no       text,
  tracking_url      text,
  line_items        jsonb not null default '[]',
  ordered_at        timestamptz,
  fetched_at        timestamptz,
  check (order_no is not null or store_bill_no is not null)
);
create unique index orders_order_no_uq on orders (order_no)      where channel = 'online';
create unique index orders_bill_no_uq  on orders (store_bill_no) where channel = 'store';

-- ───────────────────────── Tickets ─────────────────────────
create table ticket_counters (year int primary key, last_number int not null default 0);

-- Atomic, gap-free per year (IST calendar year). Format: <prefix>-<year>-<5 digits>
create function next_ticket_no() returns text language plpgsql as $$
declare y int := extract(year from (now() at time zone 'Asia/Kolkata'))::int;
        n int;
        p text := coalesce((select value->>'ticket_prefix' from settings where key = 'store'), 'MF');
begin
  insert into ticket_counters as t (year, last_number) values (y, 1)
  on conflict (year) do update set last_number = t.last_number + 1
  returning last_number into n;
  return p || '-' || y || '-' || lpad(n::text, 5, '0');
end $$;

create table complaints (
  id                 uuid primary key default gen_random_uuid(),
  ticket_no          text not null unique default next_ticket_no(),
  customer_id        uuid not null references customers(id),
  order_id           uuid references orders(id),
  purchase_channel   purchase_channel not null,
  source             complaint_source not null,
  created_via        text not null check (created_via in ('public_form','staff')),
  created_by         uuid references profiles(id),             -- null for public form
  category_id        uuid not null references categories(id),  -- leaf; parent via categories.parent_id
  description        text not null check (char_length(description) between 10 and 4000),
  priority           priority_level not null default 'medium',
  status             complaint_status not null default 'new',
  assigned_to        uuid references profiles(id),
  customer_lang      lang_code not null default 'en',          -- language for messages to the customer
  rejection_reason   text,
  -- SLA
  ack_due_at         timestamptz,
  resolve_due_at     timestamptz,
  acknowledged_at    timestamptz,
  resolved_at        timestamptz,
  closed_at          timestamptz,
  sla_paused_at      timestamptz,                              -- set while Waiting on Customer (if rule enabled)
  sla_paused_total   interval not null default '0',
  last_customer_activity_at timestamptz,                       -- drives 7-day auto-close
  reopened_count     int not null default 0,
  -- soft delete (owner only)
  deleted_at         timestamptz,
  deleted_by         uuid references profiles(id),
  created_at         timestamptz not null default now(),
  updated_at         timestamptz not null default now(),
  check (status <> 'rejected' or rejection_reason is not null)  -- reason mandatory
);
create index on complaints (status) where deleted_at is null;
create index on complaints (assigned_to, status) where deleted_at is null;
create index on complaints (customer_id);
create index on complaints (created_at desc);
create index on complaints (resolve_due_at) where status not in ('resolved','closed','rejected');

-- A complaint can involve several products (e.g. a co-ord set + dupatta)
create table complaint_items (
  id                  uuid primary key default gen_random_uuid(),
  complaint_id        uuid not null references complaints(id),
  product_id          uuid references products(id),
  product_name        text not null,
  sku                 text,
  variant             text,                    -- size / colour
  qty                 int not null default 1,
  unit_price          numeric(12,2),
  supplier_id         uuid references suppliers(id),
  batch_ref           text,                    -- lot / purchase-bill ref → spot defective batches
  shopify_line_item_id bigint
);
create index on complaint_items (complaint_id);
create index on complaint_items (sku);
create index on complaint_items (supplier_id);

create table resolutions (
  id               uuid primary key default gen_random_uuid(),
  complaint_id     uuid not null references complaints(id),
  type             resolution_type not null,
  refund_is_partial boolean,
  amount           numeric(12,2) check (amount is null or amount >= 0),  -- refund / store credit / exchange value
  coupon_code      text,
  approval         approval_status not null default 'not_required',
  requested_by     uuid references profiles(id),
  approved_by      uuid references profiles(id),
  approved_at      timestamptz,
  payout_reference text,                       -- Razorpay refund id / UPI ref
  notes            text,
  created_at       timestamptz not null default now(),
  check (type <> 'refund' or amount is not null)
);
create index on resolutions (complaint_id);

-- Single append-only timeline = audit log + conversation (internal notes vs customer-visible)
create table complaint_events (
  id           uuid primary key default gen_random_uuid(),
  complaint_id uuid not null references complaints(id),
  type         event_type not null,
  visibility   event_visibility not null default 'internal',
  actor_kind   actor_kind not null,
  actor_id     uuid references profiles(id),
  message      text,
  old_value    jsonb,
  new_value    jsonb,
  created_at   timestamptz not null default now()
);
create index on complaint_events (complaint_id, created_at);

create function forbid_event_change() returns trigger language plpgsql as $$
begin raise exception 'complaint_events is append-only'; end $$;
create trigger complaint_events_immutable
  before update or delete on complaint_events
  for each row execute function forbid_event_change();

create table complaint_attachments (
  id            uuid primary key default gen_random_uuid(),
  complaint_id  uuid not null references complaints(id),
  event_id      uuid references complaint_events(id),
  storage_path  text not null,                 -- private bucket; served via short-lived signed URLs
  mime_type     text not null check (mime_type in ('image/jpeg','image/png','image/webp','video/mp4','video/quicktime')),
  size_bytes    int  not null check (size_bytes <= 26214400),
  uploaded_by_kind actor_kind not null,
  uploaded_by   uuid references profiles(id),
  created_at    timestamptz not null default now()
);
create index on complaint_attachments (complaint_id);

create table feedback (
  id           uuid primary key default gen_random_uuid(),
  complaint_id uuid not null unique references complaints(id),
  token        text not null unique default encode(gen_random_bytes(16), 'hex'),
  rating       smallint check (rating between 1 and 5),
  comment      text,
  sent_at      timestamptz,
  submitted_at timestamptz
);

-- ───────────────────────── SLA ─────────────────────────
create table sla_rules (
  priority                   priority_level primary key,
  ack_minutes                int not null,
  resolve_minutes            int not null,
  business_hours_only        boolean not null default true,
  pause_when_waiting_customer boolean not null default true
);
-- Proposed defaults (editable): urgent 30m/24h · high 2h/48h · medium 4h/96h · low 8h/168h

-- ───────────────────────── Messaging ─────────────────────────
create table message_templates (
  id                   uuid primary key default gen_random_uuid(),
  key                  text not null,          -- ticket_created, status_changed, resolved, closed_feedback, owner_daily_summary …
  channel              notify_channel not null,
  lang                 lang_code not null,
  subject              text,                   -- email only
  body                 text not null,          -- supports {{ticket_no}} {{customer_name}} {{status}} {{track_url}} …
  wa_template_name     text,                   -- name registered with Meta/BSP
  wa_template_status   text check (wa_template_status in ('draft','pending','approved','rejected')),
  is_active            boolean not null default true,
  unique (key, channel, lang)
);

create table notification_outbox (
  id            uuid primary key default gen_random_uuid(),
  complaint_id  uuid references complaints(id),
  event_id      uuid references complaint_events(id),
  channel       notify_channel not null,
  to_address    text not null,                 -- +91… or email
  template_key  text not null,
  lang          lang_code not null,
  rendered_body text not null,
  status        notify_status not null default 'queued',
  attempts      int not null default 0,
  provider_msg_id text,
  error         text,
  scheduled_at  timestamptz not null default now(),
  sent_at       timestamptz,
  unique (complaint_id, event_id, channel)     -- idempotent: never double-send the same event
);
create index on notification_outbox (status, scheduled_at);

-- ───────────────────────── Abuse protection ─────────────────────────
create table rate_limits (                     -- public form / tracking lookups, keyed by hashed IP + mobile
  key          text not null,
  window_start timestamptz not null,
  hits         int not null default 1,
  primary key (key, window_start)
);

-- ───────────────────────── Row-level security (outline) ─────────────────────────
-- Public users NEVER touch tables directly: the public form / tracking page call
-- server routes that use the service key and return only whitelisted fields.
create function is_owner() returns boolean language sql stable security definer as
$$ select exists (select 1 from profiles where id = auth.uid() and role = 'owner' and is_active and deleted_at is null) $$;
create function is_staff() returns boolean language sql stable security definer as
$$ select exists (select 1 from profiles where id = auth.uid() and is_active and deleted_at is null) $$;

alter table complaints        enable row level security;
alter table complaint_events  enable row level security;
-- (same pattern for every table above; shown for the two most sensitive)

-- Read: [DECISION 5] default = all active staff can read all complaints (needed to catch duplicates / walk-ins)
create policy complaints_read   on complaints for select using (is_staff() and deleted_at is null);
create policy complaints_insert on complaints for insert with check (is_staff());
-- Update: owner anywhere; staff only when assigned to them or unassigned
create policy complaints_update on complaints for update using (
  is_owner() or (is_staff() and (assigned_to = auth.uid() or assigned_to is null)));
-- No DELETE policy anywhere → hard delete impossible; "delete" = owner sets deleted_at.
create policy events_read   on complaint_events for select using (is_staff());
create policy events_insert on complaint_events for insert with check (is_staff());
-- Refund approval (resolutions.approval → approved/denied) is restricted to owner in the API + a CHECK trigger.
