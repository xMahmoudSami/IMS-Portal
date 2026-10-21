# 📦 Enterprise Inventory Management System (IMS) & POS Suite

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-MVC%20%26%20Web%20API-blue?logo=dotnet)](https://dotnet.microsoft.com/apps/aspnet)
[![Entity Framework Core](https://img.shields.io/badge/EF%20Core-Code--First%2010.0-512BD4)](https://docs.microsoft.com/ef/core/)
[![Database](https://img.shields.io/badge/SQL%20Server-Local--First%20Relational-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Security](https://img.shields.io/badge/Security-Identity%20RBAC%20%7C%20Dual%20Auth-brightgreen?logo=auth0&logoColor=white)](#security-identity--rbac-matrix)
[![Automation](https://img.shields.io/badge/Webhooks-HMAC--SHA256%20%7C%20n8n-FF6D5A?logo=n8n&logoColor=white)](#event-driven-webhook-dispatcher-n8n-integration)
[![AI Assistant](https://img.shields.io/badge/GenAI-RAG%20%7C%20Groq%20Llama%203.3-F05032?logo=meta&logoColor=white)](#generative-ai-assistant-rag-pipeline)
[![Localization](https://img.shields.io/badge/i18n-6%20Languages%20%7C%20RTL%20Native-informational)](#internationalization--ui-theme-engine)

A local-first, production-ready **Enterprise Inventory Management System (IMS)** and **Point-of-Sale (POS)** suite engineered with **ASP.NET Core 10** and **Entity Framework Core**. Built for high data integrity, strict auditability, role-based governance, and real-time event-driven automation.

---

## 📑 Table of Contents

- [Product Overview](#product-overview)
- [Key Enterprise Capabilities](#key-enterprise-capabilities)
- [System Architecture](#system-architecture)
- [Security, Identity & RBAC Matrix](#security-identity--rbac-matrix)
- [Master Recovery Key & Zero-SMTP Architecture](#master-recovery-key--zero-smtp-architecture)
- [Event-Driven Webhook Dispatcher (n8n Integration)](#event-driven-webhook-dispatcher-n8n-integration)
- [Point of Sale (POS) & Transactional Pipeline](#point-of-sale-pos--transactional-pipeline)
- [Generative AI Assistant (RAG Pipeline)](#generative-ai-assistant-rag-pipeline)
- [Internationalization & UI Theme Engine](#internationalization--ui-theme-engine)
- [Database Architecture (ERD & Schema)](#database-architecture-erd--schema)
- [Getting Started & Local Deployment](#getting-started--local-deployment)
- [Technical Documentation Suite](#technical-documentation-suite)

---

<a name="product-overview"></a>

## 📌 Product Overview

The **Enterprise Inventory Management System (IMS)** provides centralized operational oversight for modern retail, wholesale, and multi-tier distribution environments. Combining high-speed Point-of-Sale (POS) order fulfillment with procurement automation, vendor price-contract evaluation, and stock ledger reconciliation, the platform enforces strict business rules including **zero-negative-stock invariants**, **preventative delete restrictions**, and **atomic database transactions**.

Equipped with a dual-authentication security pipeline (Cookie + JWT Bearer), cryptographic Master Recovery Key infrastructure, and signed webhook broadcasting to external workflow engines like **n8n**, the system is designed to operate securely in air-gapped, local-first, or cloud-hosted deployments.

---

<a name="key-enterprise-capabilities"></a>

## 🌟 Key Enterprise Capabilities

- **Atomic Inventory Control**: Real-time stock increments on procurement check-in and instantaneous reductions on point-of-sale customer checkouts with deterministic concurrency safety.
- **Role-Based Access Governance (RBAC)**: 5 enterprise roles with policy-based permission claims enforcing strict operational separation between cashiers, inventory managers, procurement specialists, and financial auditors.
- **Zero-SMTP Master Recovery Pipeline**: High-entropy 128-bit cryptographic Master Recovery Keys and PBKDF2 hashed verification, eliminating dependency on external email servers for credential recovery.
- **Event-Driven Webhook Engine**: Cryptographically signed (`HMAC-SHA256`) event payloads automatically dispatched to external endpoints (e.g. n8n automation nodes for instant Telegram and messaging alerts).
- **Interactive POS Dynamic Checkout**: Real-time barcode/SKU selection, live quantity validation against on-hand inventory, dynamic subtotaling, and price snapshotting.
- **Supplier & Multi-Vendor Relationship Mapping**: Many-to-Many junction mapping between suppliers and products with custom contract pricing, supplier performance ratings, and lead times.
- **Enterprise Reporting & Analytics**: Dynamic inventory valuation, fast-moving vs slow-moving stock identification, and sales trend visualization via Chart.js.
- **Context-Aware AI Assistant (RAG)**: Natural-language enterprise inventory queries powered by Retrieval-Augmented Generation (RAG) over live relational database entities via Groq Cloud LLMs.
- **Global Multi-Language & Native RTL**: Complete localization across 6 global languages (`en`, `ar`, `fr`, `es`, `it`, `de`) with automated CSS mirroring for Arabic.
- **Anti-Flicker Synchronous Theme Engine**: Dark and Light theme presets dynamically loaded before CSS rendering to prevent screen flash.

---

<a name="system-architecture"></a>

## 🏛️ System Architecture

The solution follows clean architectural principles, strictly separating concerns across the Presentation, Controller, Service, and Data Access layers:

```mermaid
graph TD
    Client[Web Browser / POS Handhelds / External APIs] -->|HTTP / HTTPS| Gate[Dual Authentication Gateway<br/>Cookie Auth + JWT Bearer Auth]
    Gate --> Presentation[Presentation Layer<br/>Razor Views + SB Admin 2 + Dynamic Theme Engine]
    Presentation --> Controllers[Controller Layer<br/>MVC Controllers + RESTful APIs + Anti-CSRF]
    Controllers --> ViewModels[Presentation Models Layer<br/>Strict DTOs + PagedResult + Custom Validation]
    Controllers --> Services[Domain Services / Business Logic Layer<br/>IProductService, ICategoryService, ISupplierService, IAIService]
    Controllers --> Recovery[Master Recovery Key Service<br/>IMasterRecoveryKeyService]
    Controllers --> Dispatcher[Event-Driven Webhook Dispatcher<br/>IWebhookDispatcher + IWebhookSigner]

    Services -->|RAG Context & LLM Prompting| Groq[Groq Cloud LLM API<br/>Llama 3.3 70B Versatile]
    Dispatcher -->|HMAC-SHA256 Signed HTTP POST| n8n[n8n Workflow Automation Engine<br/>Telegram / WhatsApp / Discord]
    Services --> EF[Data Access Layer<br/>Entity Framework Core 10.0]
    Recovery --> EF
    EF --> Database[(Relational Database<br/>Microsoft SQL Server)]
    Presentation --> Localization[Localization Engine<br/>SharedResource .resx (EN, AR, FR, ES, IT, DE)]
```

---

<a name="security-identity--rbac-matrix"></a>

## 🔒 Security, Identity & RBAC Matrix

The system implements a production-ready security baseline utilizing **ASP.NET Core Identity** and **Entity Framework Core**:

### Dual Authentication Architecture

1. **Cookie Authentication**: Secure, HTTP-only, SameSite cookies for web portal interactive sessions. Automatic redirection to `/Account/Login` and `/Account/AccessDenied`.
2. **JWT Bearer Authentication**: HMAC-SHA256 signed JSON Web Tokens issued via `/api/Auth/login` for headless clients, mobile terminals, and external API integrations.

### Access Control Matrix

| Enterprise Role           | Target Persona                 | Operational Scope & Permission Boundaries                                                                                                    |
| :------------------------ | :----------------------------- | :------------------------------------------------------------------------------------------------------------------------------------------- |
| **SuperAdmin**            | System Administrator / Owner   | Complete system authority. User account provisioning, role assignments, financial audit inspections, profile security, and key regeneration. |
| **WarehouseManager**      | Logistics & Warehouse Lead     | Complete catalog management (Products, Categories, Suppliers, Supplier Mappings) and Stock-In Purchase orders. Restricted from POS sales.    |
| **Cashier**               | Frontline Store Staff          | High-speed POS sales terminal, item lookup, invoice generation, and receipt printing. No visibility into vendor pricing or profit margins.   |
| **ProcurementSpecialist** | Purchasing Agent               | Supplier directory management, purchase order generation, contract price evaluation, and delivery tracking. Excluded from retail sales.      |
| **Auditor**               | Financial & Compliance Officer | Read-only visibility across Financial Reports, Stock Valuation, Sales Performance, and historical invoices. Cannot modify database records.  |

---

<a name="master-recovery-key--zero-smtp-architecture"></a>

## 🔑 Master Recovery Key & Zero-SMTP Architecture

Traditional self-hosted systems often fail when external SMTP mail servers are unreachable or unconfigured. To ensure complete autonomy, the platform utilizes a **Zero-SMTP Master Recovery Key** pipeline:

1. **High-Entropy Generation**: Cryptographically secure 128-bit random keys formatted as readable chunks (`IMS-XXXX-XXXX-XXXX-XXXX`).
2. **Cryptographic Storage**: Raw keys are never stored in the database. Only salted PBKDF2 hashes (via ASP.NET Core Identity's password hasher) are persisted.
3. **Strict One-Time Display Policy**: Plain-text keys are shown to administrators strictly **once** upon initial generation or user provisioning.
4. **Self-Service Credential Reset (`/Account/ForgotPassword`)**:
   - Step 1: User enters their username or email.
   - Step 2: System validates the provided Master Recovery Key against the salted PBKDF2 hash.
   - Step 3: Upon verification, the user sets a new password, and the system executes **single-use rotation**, generating a fresh recovery key.
5. **On-Demand Profile Regeneration**: Authenticated users can regenerate their Master Recovery Key at any time from their Security Profile, invalidating previous keys.

---

<a name="event-driven-webhook-dispatcher-n8n-integration"></a>

## ⚡ Event-Driven Webhook Dispatcher (n8n Integration)

The application incorporates a resilient, asynchronous event dispatcher for automated workflow execution:

```
┌─────────────────────────────────┐
│     ASP.NET Core Application    │
│  (Domain Events & Transactions) │
└────────────────┬────────────────┘
                 │
                 │ HTTP POST + X-Hub-Signature-256 (HMAC-SHA256)
                 ▼
┌─────────────────────────────────┐
│      n8n Automation Engine      │
│     (Event Parsing & Routing)   │
└────────┬───────────────┬────────┘
         │               │
         ▼               ▼
┌─────────────────┐  ┌──────────────────┐
│  Telegram Node  │  │  External Webhook│
│(Admin Channels) │  │  (ERP / Logistics│
└─────────────────┘  └──────────────────┘
```

### Registered Event Hooks

- `auth.password_reset_requested`: Dispatched when password recovery is initiated. Generates a temporary 6-digit emergency OTP delivered to administrator Telegram channels via n8n.
- `inventory.low_stock_alert`: Automatically triggered whenever POS sales or inventory adjustments reduce product quantities to or below their configured `LowStockThreshold`.

### Dispatcher Architecture

- **Cryptographic Request Signing**: Every outgoing JSON envelope is signed using HMAC-SHA256 (`IWebhookSigner`) and attached via the `X-Hub-Signature-256` HTTP header.
- **Graceful Fault Tolerance**: Operates asynchronously using `IHttpClientFactory`. Network timeouts or unreachable endpoints are logged safely without interrupting core user workflows or database transactions.

---

<a name="point-of-sale-pos--transactional-pipeline"></a>

## 🛒 Point of Sale (POS) & Transactional Pipeline

The transaction engine guarantees strict financial and inventory ledger integrity:

- **Procurement Orders (Purchases)**: Multi-line invoice creation with dynamic supplier contract pricing, automatically incrementing warehouse stock on commit.
- **POS Checkout (Sales)**: Fast-response storefront interface featuring live SKU lookup, real-time inventory threshold verification, and automatic stock deduction.
- **Zero-Negative-Stock Invariant**: Sales transactions attempting to deduct more stock than physically available on hand are rejected at the service and database boundary.
- **Safe Transaction Reversion**: Deleting or voiding an invoice safely reverses stock changes within an isolated database transaction.

---

<a name="generative-ai-assistant-rag-pipeline"></a>

## 🤖 Generative AI Assistant (RAG Pipeline)

The embedded **AI Inventory Assistant** enables non-technical operators to interact with complex inventory data using natural language:

- **Retrieval-Augmented Generation (RAG)**: Extracts live entity records (top products, low-stock items, supplier contracts, financial aggregations) directly from SQL Server and injects them into the prompt context.
- **High-Speed Inference**: Powered by the Groq Cloud API running `llama-3.3-70b-versatile` with automated fallback to `llama-3.1-8b-instant`.
- **Cross-Site Scripting (XSS) Sanitization**: AI responses are rendered using `marked.js` and scrubbed through `DOMPurify` before DOM insertion to guarantee client security.
- **Conversation Audit Trail**: All customer queries and AI answers are audited in the `AIChatLogs` database table.

---

<a name="internationalization--ui-theme-engine"></a>

## 🌐 Internationalization & UI Theme Engine

- **6 Supported Cultures**: English (`en`), Arabic (`ar`), French (`fr`), Spanish (`es`), Italian (`it`), and German (`de`).
- **Dynamic RTL Layout Mirroring**: Selecting Arabic automatically switches layout direction to RTL, flipping navigation drawers, tables, and alignment grids.
- **Token-Based Theme Engine**: Built with CSS Custom Properties (`--bg-base`, `--text-main`, `--primary-gradient`). Theme state is evaluated synchronously in the `<head>` to prevent white-flash loading.

---

<a name="database-architecture-erd--schema"></a>

## 📐 Database Architecture (ERD & Schema)

The database schema is managed via Entity Framework Core Code-First migrations on Microsoft SQL Server:

```
Category (1) ────< Product (M) >──── (M) SupplierProduct (M) >──── (1) Supplier
                      │                                                   │
                      ├─────────< SaleItem (M) >────── Sale (1)           │
                      │                                                   │
                      └─────────< PurchaseItem (M) >── Purchase (1) ──────┘
```

![Database ERD](Docs/ERD.svg)

### Referential Integrity Constraints

- `Product.SKU`: Unique database index.
- `SupplierProduct`: Composite unique index on `(SupplierID, ProductID)`.
- `DeleteBehavior.Restrict`: Enforced on `Product`, `Category`, and `Supplier` to preserve historical transaction audit trails.

---

<a name="getting-started--local-deployment"></a>

## 🚀 Getting Started & Local Deployment

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Microsoft SQL Server](https://www.microsoft.com/sql-server) (LocalDB, SQL Express, or Standard)

### Configuration Setup

Configure your database connection and credentials in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=InventoryManagementDB;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "YOUR_SUPER_SECRET_HMAC_SHA256_KEY_AT_LEAST_32_CHARS_LONG!",
    "Issuer": "InventoryManagementSystem",
    "Audience": "InventoryManagementSystemClient",
    "ExpireMinutes": 480
  },
  "WebhookSettings": {
    "Enabled": true,
    "PayloadUrl": "https://your-n8n-instance.domain/webhook/ims-events",
    "Secret": "your_cryptographic_hmac_secret_sha256_key",
    "TimeoutSeconds": 10
  },
  "DefaultAdmin": {
    "Username": "admin",
    "Email": "admin@ims.local",
    "FullName": "System Administrator",
    "Password": "Admin@12345",
    "RecoveryKey": "IMS-ADMIN-RECOVERY-KEY-2026"
  }
}
```

### Execution & Automatic Migration

Execute the application from the project root:

```bash
cd Inventory_Management_System
dotnet run
```

> [!NOTE]
> On startup, `Program.cs` automatically executes `await context.Database.MigrateAsync()` and seeds standard roles, claims, and default accounts. No manual SQL script execution is required.

### Default System Accounts

| Role                      | Username    | Default Password  | Initial Master Recovery Key   |
| :------------------------ | :---------- | :---------------- | :---------------------------- |
| **SuperAdmin**            | `admin`     | `Admin@12345`     | `IMS-ADMIN-RECOVERY-KEY-2026` |
| **WarehouseManager**      | `warehouse` | `Warehouse@12345` | `IMS-WAREHOUSE-KEY-2026`      |
| **Cashier**               | `cashier`   | `Cashier@12345`   | `IMS-CASHIER-KEY-2026`        |
| **ProcurementSpecialist** | `purchaser` | `Purchaser@12345` | `IMS-PURCHASER-KEY-2026`      |
| **Auditor**               | `auditor`   | `Auditor@12345`   | `IMS-AUDITOR-KEY-2026`        |

---

<a name="technical-documentation-suite"></a>

## 📚 Technical Documentation Suite

For deeper architectural analysis, refer to the specialized documentation modules:

- **[Master Technical Guide & Architecture](Docs/README.md)**: Full system blueprint, sitemap, technology stack, and security highlights.
- **[Data & Domain Models](Docs/Models/MODELS_README.md)**: Primary entities, ERD, unique indexes, decimal precision, and EF Core cascade/restrict rules.
- **[Presentation Models & ViewModels](Docs/ViewModels/ViewModels_README.md)**: Anti-mass assignment defense, validation rules, generic pagination, and delete checks.
- **[Controllers & API Endpoints](Docs/Controllers/CONTROLLERS_README.md)**: Catalog of 13 MVC & API controllers, RBAC matrix, and HTTP routing specifications.
- **[Services & Business Logic](Docs/Services/SERVICES_README.md)**: Interface contracts, transactional invariants, RAG AI integration, and Master Recovery Key pipeline.
- **[User Interface & Views](Docs/Views/Views_README.md)**: Razor architecture, dynamic POS calculations, responsive theme engine, and accessible dialog modals.
- **[Internationalization & i18n](Docs/Localization/LOCALIZATION_README.md)**: 6-culture resource localization, cookie persistence, and RTL Arabic mirroring.
- **[Validation & Data Integrity](Docs/Validations/VALIDATIONS_README.md)**: 5-tier defense-in-depth validation strategy, custom `UniqueAttribute`, and business invariants.

---

&copy; Enterprise Inventory Management System (IMS). All Rights Reserved.
