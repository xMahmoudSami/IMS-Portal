# Internationalization (i18n) & Localization Architecture (`Docs/Localization/`)

## 1. Architectural Overview

The application features an enterprise-grade internationalization and localization system supporting **6 major languages**:

| Language | Culture Code | Direction | Native Name |
| :--- | :--- | :--- | :--- |
| **English** | `en` | Left-to-Right (LTR) | English |
| **Arabic** | `ar` | Right-to-Left (RTL) | العربية |
| **French** | `fr` | Left-to-Right (LTR) | Français |
| **Spanish** | `es` | Left-to-Right (LTR) | Español |
| **Italian** | `it` | Left-to-Right (LTR) | Italiano |
| **German** | `de` | Left-to-Right (LTR) | Deutsch |

```mermaid
graph TD
    Client["Browser Request"] -->|"Cookie: .AspNetCore.Culture"| Pipeline["ASP.NET Core Localization Middleware"]
    Pipeline -->|"Matches Culture"| Providers{"RequestCultureProviders"}
    Providers -->|"1. CookieProvider (Priority)"| MatchFound["Set CurrentCulture & CurrentUICulture"]
    Providers -->|"2. QueryStringProvider"| MatchFound
    Providers -->|"3. AcceptLanguageHeader"| MatchFound
    MatchFound -->|"Injects"| Views["Views & Controllers<br/>IHtmlLocalizer / IStringLocalizer"]
    Views -->|"Pulls String"| Resx[("Resources/SharedResource.{culture}.resx")]
```

---

## 2. Resource Files Structure (`Resources/`)

Localization strings are centralized using standard .NET XML Resource (`.resx`) files under the `Resources/` directory:

```
Resources/
├── SharedResource.resx       # Neutral / Fallback Default (English)
├── SharedResource.en.resx    # English
├── SharedResource.ar.resx    # Arabic (Full RTL translations)
├── SharedResource.fr.resx    # French
├── SharedResource.es.resx    # Spanish
├── SharedResource.it.resx    # Italian
└── SharedResource.de.resx    # German
```

### Strongly-Typed Marker Class (`SharedResource.cs`):
```csharp
namespace Inventory_Management_System
{
    /// <summary>
    /// Dummy marker class used to group shared localization resources.
    /// </summary>
    public class SharedResource
    {
    }
}
```
This marker class allows ASP.NET Core dependency injection to bind resource files to `IHtmlLocalizer<SharedResource>` and `IStringLocalizer<SharedResource>`.

---

## 3. Pipeline Configuration (`Program.cs`)

The localization infrastructure is configured in `Program.cs` across three distinct phases:

### 3.1. Service Registration:
```csharp
// 1. Point localization engine to the "Resources" folder
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// 2. Register View and DataAnnotations Localization
var mvcBuilder = builder.Services
    .AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization(options => {
        options.DataAnnotationLocalizerProvider = (type, factory) =>
            factory.Create(typeof(Inventory_Management_System.SharedResource));
    });
```
- **DataAnnotations Localization:** Automatically translates model validation error messages (e.g., `[Required]`, `[Range]`, `[StringLength]`) using entries in `SharedResource.{culture}.resx`.

---

### 3.2. Provider Priority Order:
```csharp
var supportedCultures = new[] { "en", "ar", "fr", "es", "it", "de" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

// Explicit provider order: Cookie takes top precedence
locOptions.RequestCultureProviders.Clear();
locOptions.RequestCultureProviders.Add(new CookieRequestCultureProvider());
locOptions.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
locOptions.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());

// Middleware must run BEFORE routing
app.UseRequestLocalization(locOptions);
```
- **Why Clear and Re-add Providers?**
  By explicitly placing `CookieRequestCultureProvider` first, user-selected preferences always override browser headers.

---

## 4. Language Selection Flow (`CultureController.cs`)

When the user selects a language in the header dropdown, a POST request is submitted to `CultureController`:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult SetLanguage(string culture, string returnUrl)
{
    // 1. Whitelist validation
    if (!_supportedCultures.Contains(culture))
    {
        culture = "en";
    }

    // 2. Persist in cookie with 1-year expiration
    Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax
        }
    );

    // 3. Prevent Open Redirect Attacks
    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
    {
        return LocalRedirect(returnUrl);
    }

    return LocalRedirect("~/");
}
```

---

## 5. View Consumption Patterns: HTML vs. JavaScript Context

In `Views/_ViewImports.cshtml`, the localizer is made globally available to all views:
```razor
@inject IHtmlLocalizer<SharedResource> Loc
```

### Critical Engineering Rule:
- **Inside HTML Elements:** Use `@Loc["Key"]`.
  ```html
  <h1>@Loc["Nav_Products"]</h1>
  <button type="button">@Loc["Save"]</button>
  ```
  Razor automatically renders the `LocalizedHtmlString` through `IHtmlContent.WriteTo`.

- **Inside JavaScript / Script Blocks:** ALWAYS use `.Value` and `JavaScriptEncoder`.
  ```javascript
  // DO NOT DO THIS (Produces "Microsoft.AspNetCore.Mvc.Localization.LocalizedHtmlString"):
  // const msg = '@Html.Raw(Loc["Confirm"])';

  // CORRECT PATTERN:
  const confirmText = '@Html.Raw(System.Text.Encodings.Web.JavaScriptEncoder.Default.Encode(Loc["Confirm"].Value))';
  ```
  - **Reason:** `Loc["Key"]` returns an object of type `LocalizedHtmlString`. When passed to `@Html.Raw(...)`, C# invokes `object.ToString()`, which emits the CLR class name instead of the translated text. Using `.Value` accesses the underlying localized string, and `JavaScriptEncoder` ensures that single quotes, accents, or Arabic characters do not break the JavaScript literal.

---

## 6. Culture Formatting vs. Invariant Currency Parsing

Different cultures format decimals and dates differently:
- **English (`en`):** Uses period for decimal (`1234.56`) and comma for thousands (`1,234.56`).
- **French (`fr`) / German (`de`) / Spanish (`es`) / Italian (`it`):** Uses comma for decimal (`1234,56`) and space/dot for thousands.

### Architectural Solution for Client-Side Math:
When numeric values (prices, costs, stock counts) are used in client-side JavaScript routines (such as in the POS Sales checkout or Purchase order builder):

1. **Human Display:**
   Formatted according to current culture: `@item.UnitPrice.ToString("C")` or `@item.UnitPrice.ToString("N2")`.
2. **Machine Calculation:**
   Rendered using `CultureInfo.InvariantCulture` into HTML `data-*` attributes:
   ```html
   <option value="@p.ProductID" data-price="@p.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)">
       @p.ProductName - @p.UnitPrice.ToString("N2")
   </option>
   ```
   In JavaScript, `parseFloat($(opt).data('price'))` always receives a standard dot-separated decimal (e.g. `24.99`), completely avoiding `NaN` calculation errors in French, German, or Spanish environments.
