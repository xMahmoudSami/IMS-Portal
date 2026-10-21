# Enterprise Inventory Management System (IMS) — Technical Architecture Documentation

Welcome to the comprehensive technical documentation for the **Enterprise Inventory Management System (IMS) & POS Suite**. This documentation suite provides an in-depth, engineering-grade analysis of every architectural layer, design pattern, security measure, and implementation detail within the system.

---

## 🏛️ High-Level System Architecture

The application is engineered using **ASP.NET Core (.NET 10 / C#)** following clean architectural principles: **Separation of Concerns (SoC)**, **Dependency Inversion (SOLID)**, **Repository/Service Layer Abstraction**, and **Domain-Driven Design (DDD)** principles for inventory data integrity.

```mermaid
graph TD
    Client[Web Browser / POS Handhelds / External APIs] -->|HTTP / HTTPS| Gate[Dual Authentication Gateway<br/>Cookie Auth + JWT Bearer Auth]
    Gate --> Presentation[Presentation Layer<br/>Razor Views + SB Admin 2 + Dynamic Theme Engine]
    Presentation --> Controllers[Controller Layer<br/>MVC Controllers + RESTful API Controllers]
    Controllers --> ViewModels[Presentation Models Layer<br/>ViewModels + PagedResult + Validation Attributes]
    Controllers --> Services[Service Layer / Business Logic<br/>ICategoryService, IProductService, ISupplierService, IAIService]
    Controllers --> Recovery[Master Recovery Key Pipeline<br/>IMasterRecoveryKeyService]
    Controllers --> Dispatcher[Event-Driven Webhook Dispatcher<br/>IWebhookDispatcher + IWebhookSigner]

    Services -->|RAG Analytics & LLM Completion| Groq[Groq Cloud API<br/>Llama 3.3 70B Versatile]
    Dispatcher -->|HMAC-SHA256 Signed HTTP POST| n8n[n8n Workflow Automation Engine<br/>Telegram / External Webhooks]
    Services --> DataAccess[Data Access Layer<br/>EF Core 10 + ApplicationDbContext]
    Recovery --> DataAccess
    DataAccess -->|SQL Server Protocol| Database[(Relational Database<br/>Microsoft SQL Server)]
    Presentation -->|Culture Selection| Localization[Localization Engine<br/>SharedResource .resx (EN, AR, FR, ES, IT, DE)]
```

---

## 📂 Documentation Structure & Sitemap

Each architectural layer is documented in a dedicated deep-dive guide located in its respective subdirectory:

| Layer                              | Module Guide                                                               | Primary Topics Covered                                                                                                                                                                                                                                                                               |
| :--------------------------------- | :------------------------------------------------------------------------- | :--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **1. Data & Entities**             | [Models/MODELS_README.md](Models/MODELS_README.md)                         | The 9 Domain Entities (`Product`, `Category`, `Supplier`, `SupplierProduct`, `Purchase`, `PurchaseItem`, `Sale`, `SaleItem`, `AIChatLog`), Foreign Keys, Data Annotations, `ApplicationDbContext`, Fluent API decimal precision, Unique Constraints, and Delete Behaviors (`Cascade` vs `Restrict`). |
| **2. Presentation Models**         | [ViewModels/ViewModels_README.md](ViewModels/ViewModels_README.md)         | Domain vs View Model separation, Mass-assignment & Over-posting defense, UI-specific validation, Generic Pagination (`PagedResult<T>`), Command Result encapsulation (`OperationResult`), and Delete guard checking.                                                                                 |
| **3. Controllers & Endpoints**     | [Controllers/CONTROLLERS_README.md](Controllers/CONTROLLERS_README.md)     | Breakdown of MVC & API Controllers, HTTP routing conventions (`[HttpGet]`, `[HttpPost]`, `[HttpDelete]`), Dependency Injection patterns, Anti-Forgery token enforcement (`[ValidateAntiForgeryToken]`), and response handling.                                                                       |
| **4. Services & Business Logic**   | [Services/SERVICES_README.md](Services/SERVICES_README.md)                 | Service layer interfaces (`ICategoryService`, `IProductService`, `ISupplierService`, `IAIService`), loose coupling, transactional stock management, Groq LLM integration, Retrieval-Augmented Generation (RAG) analytics pipeline, and fallback safeguards.                                          |
| **5. User Interface & Frontend**   | [Views/Views_README.md](Views/Views_README.md)                             | Modular Razor layout structure, POS Sales interface with dynamic client calculations, Purchases order builder, Chart.js Analytics dashboard, CSS custom properties Theme Engine (Dark/Light modes), dynamic RTL Arabic mirroring, and native alert replacement modal.                                |
| **6. Internationalization (i18n)** | [Localization/LOCALIZATION_README.md](Localization/LOCALIZATION_README.md) | Multi-language infrastructure supporting 6 cultures (`en`, `ar`, `fr`, `es`, `it`, `de`), resource synchronization (`SharedResource.*.resx`), Cookie Provider precedence, RTL text-direction handling, and culture vs invariant currency parsing.                                                    |

---

## 🛠️ Technology Stack & Core Specifications

- **Framework:** ASP.NET Core 10.0 (C# 13)
- **Object-Relational Mapper (ORM):** Entity Framework Core 10.0 (SQL Server Provider)
- **Database Engine:** Microsoft SQL Server
- **Authentication & Identity:** ASP.NET Core Identity with Dual Authentication (HTTP-only Cookies & HMAC-SHA256 JWT Bearer)
- **Credential Recovery:** Zero-SMTP 128-bit Master Recovery Key pipeline with salted PBKDF2 hashing
- **Automation & Webhooks:** Asynchronous Webhook Dispatcher with HMAC-SHA256 payload signing (`X-Hub-Signature-256`) for n8n integration
- **AI / LLM Integration:** Groq Cloud API (`llama-3.3-70b-versatile` / `llama-3.1-8b-instant`) with RAG database context injection
- **Frontend Architecture:** Razor Views (.cshtml), HTML5, Vanilla CSS3 Custom Properties (CSS Tokens), jQuery 3.6, Bootstrap 4.6 (SB Admin 2)
- **Data Visualization:** Chart.js 2.9 (Line, Bar, Doughnut charts)
- **Markdown & Security Sanitization:** `marked.js` + `DOMPurify` for real-time AI reply rendering
- **Internationalization:** ASP.NET Core Localization (`IHtmlLocalizer`, `IStringLocalizer`, `IViewLocalizer`)

---

## 🔒 Enterprise Security & Robustness Highlights

1. **Dual Authentication Gateway:**
   Seamlessly supports cookie-based web portal navigation alongside signed JWT Bearer tokens for external API integration, POS terminals, and mobile devices.
2. **Role-Based Access Control (RBAC):**
   5 distinct operational roles (`SuperAdmin`, `WarehouseManager`, `Cashier`, `ProcurementSpecialist`, `Auditor`) backed by granular policy claims.
3. **Zero-SMTP Master Recovery Key Infrastructure:**
   Eliminates dependencies on external mail servers by utilizing 128-bit cryptographic Master Recovery Keys stored as salted PBKDF2 hashes, adhering to a strict one-time presentation policy with automatic single-use key rotation upon reset.
4. **Cryptographic Webhook Signatures (`X-Hub-Signature-256`):**
   All outgoing event notifications dispatched to n8n are cryptographically signed using HMAC-SHA256 over the payload body, ensuring authenticity and preventing tampering.
5. **Over-Posting (Mass-Assignment) Prevention:**
   All create/update controller actions exclusively accept strict ViewModels instead of entity instances, preventing malicious injection of internal IDs, timestamps, or unauthorized audit fields.
6. **Anti-CSRF Protection:**
   Every mutating HTTP POST endpoint strictly requires `[ValidateAntiForgeryToken]`, protecting users against Cross-Site Request Forgery attacks.
7. **Domain Integrity & Accidental Deletion Defense:**
   Parent tables (`Products`, `Categories`, `Suppliers`) enforce `DeleteBehavior.Restrict` in EF Core, disallowing hard deletions when active historical transactions (`PurchaseItem`, `SaleItem`) exist.
8. **XSS Prevention in AI Outputs:**
   Incoming LLM Markdown output is parsed through `marked.js` and sanitized through `DOMPurify` before DOM injection, mitigating prompt-injection and stored XSS vectors.
9. **Anti-Flicker Synchronous Theme Loading:**
   Theme preference (`dark` or `light`) is stored in `localStorage` and evaluated via a blocking `<script>` in the `<head>` prior to CSS rendering, completely eliminating UI flicker.

---

&copy; Enterprise Inventory Management System (IMS). All Rights Reserved.
