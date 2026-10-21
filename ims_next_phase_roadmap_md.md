# Enterprise Inventory Management System (IMS)
## Next-Phase Architecture & Feature Expansion Roadmap

---

## 1. Security, Identity & Role-Based Access Control (RBAC)

### 1.1 Technical Stack & Security Baseline
- **Identity Provider**: ASP.NET Core Identity integrated with Entity Framework Core.
- **Authentication Mechanisms**:
  - **Cookie Authentication**: Secure HTTP-only cookies for web portal sessions.
  - **JWT Bearer Tokens**: Signed tokens (`HMAC-SHA256` or `RSA`) to support external API consumers, mobile handhelds, and decoupled endpoints.
- **Password Policies**: Enforced entropy standards (`PBKDF2` hashing with dynamic salting).
- **Authorization Enforcement**: Policy-based requirements mapped to fine-grained claims (e.g., `Permissions.Inventory.Edit`, `Permissions.Sales.Execute`).

### 1.2 User Roles & Access Control Matrix

| Role | Target Persona | Scope of Permissions & Boundaries |
| :--- | :--- | :--- |
| **Super Admin / Business Owner** | Executive Management | Full unrestricted system control. Manages application users, role assignments, financial audit trails, tenant settings, and irreversible records. |
| **Warehouse / Inventory Manager** | Logistics Lead | Comprehensive authority over Products, Categories, Suppliers, Supplier Mappings, and Purchase Orders (Stock-In). Restricted from executing POS customer transactions. |
| **Cashier / POS Operator** | Storefront Staff | Dedicated POS checkout interface, rapid item entry, stock-out invoice generation, and receipt printing. No access to vendor purchase costs, profit margins, or catalog modification. |
| **Procurement Specialist** | Purchasing Officer | Management of supplier directories, purchase order drafting, contract pricing evaluation, and lead-time monitoring. Excluded from retail sales operations. |
| **Auditor / Financial Viewer** | Accounting & Compliance | Read-Only visibility across Financial Reports, Stock Valuation, Sales Performance metrics, and historical invoice verification. Cannot alter database records. |

---

## 2. Advanced Enterprise Feature Expansions

### 2.1 Barcode Engine & Label Printing
- **Hardware-Integrated POS Scanning**:
  - Built-in keyboard wedge input listener on the POS terminal.
  - Automatically captures incoming scanner inputs, extracts product `SKU`, and dynamically appends the item to the transaction grid with zero keyboard intervention.
- **Automated Label & Barcode Generation**:
  - Client and server-side barcode generation supporting `Code 128` and `QR Code` specifications.
  - Export layout engine producing printable adhesive label sheets tailored for warehouse shelves and product packaging.

### 2.2 Inventory Returns & Scrap Adjustments
- **Sales Return & Refund Workflow**:
  - Structured refund pipeline tied directly to original sales invoice numbers (`SaleID`).
  - Automatic inventory reversal restoring returned units to stock counts under atomic database transactions.
  - Mandatory audit notes tracking reasons (e.g., Defective Item, Customer Exchange).
- **Damage & Expiry Scrap Journal**:
  - Non-sale write-off journal permitting inventory leads to decommission damaged, expired, or obsolete goods.
  - Reconciles physical warehouse stock with digital ledger counts without distorting sales revenue figures.

### 2.3 Bulk Data Import via Excel / CSV
- **High-Throughput Batch Processing**:
  - Ingestion service utilizing `ClosedXML` / `CsvHelper` for mass seeding of products, categories, and supplier catalogs.
  - Encapsulated within isolated database transactions to ensure an all-or-nothing commit model.
- **Pre-Ingestion Validation Pipeline**:
  - Real-time pre-flight checks catching duplicate SKUs, broken foreign key references, and invalid numerical bounds prior to database execution.

### 2.4 Multi-Warehouse Architecture & Stock Transfers
- **Multi-Branch Data Structure**:
  - Introduction of a relational `Warehouse` entity distributing stock counts per facility (e.g., Central Depot, Downtown Branch, Regional Facility).
- **Inter-Warehouse Transfer Orders (STO)**:
  - Formally tracked stock movement lifecycle with dual-state status tracking:
    $$\text{Draft} \longrightarrow \text{Pending Approval} \longrightarrow \text{In-Transit} \longrightarrow \text{Received}$$
  - Full custody tracking ensuring inventory is never lost between physical locations.

---

## 3. External Webhook Automation Architecture (via n8n)

### 3.1 Event-Driven Notification Pipeline

```
┌─────────────────────────────────┐
│     ASP.NET Core Application    │
│   (Domain Events & Background)  │
└────────────────┬────────────────┘
                 │
                 │ HTTP POST (Payload Signed with HMAC Secret)
                 ▼
┌─────────────────────────────────┐
│      n8n Automation Engine      │
│     (Event Routing & Logic)     │
└────────┬───────────────┬────────┘
         │               │
         ▼               ▼
┌─────────────────┐  ┌──────────────────┐
│  Telegram Node  │  │  WhatsApp Node   │
│ (Bot API Token) │  │(Cloud API/Twilio)│
└────────┬────────┘  └─────────┬────────┘
         │                     │
         ▼                     ▼
┌─────────────────┐  ┌──────────────────┐
│ Telegram Alerts │  │ Operations Team  │
│ (Channel/Group) │  │ (Direct Messages)│
└─────────────────┘  └──────────────────┘
```

### 3.2 Integrated Automation Workflows
- **Instant Low-Stock Dispatches**:
  - Triggered automatically whenever a transaction results in $\text{StockQuantity} \le \text{LowStockThreshold}$.
  - Sends immediate actionable alerts containing Product Name, SKU, Current Count, and Primary Supplier phone link.
- **Daily Executive Digest**:
  - Scheduled background execution (via `IHostedService` or `Quartz.NET`) dispatching an end-of-day summary at 23:59.
  - Distributes total daily revenue, completed order volume, and top-selling items to managerial channels.
- **Critical Security & Fault Warnings**:
  - Captures severe server exceptions and unauthorized privilege escalation attempts, routing immediate alerts to administrative staff without manual log inspection.