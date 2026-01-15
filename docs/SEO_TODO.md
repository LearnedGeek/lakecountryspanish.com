# SEO Implementation TODO

This document tracks SEO improvements needed for the Lake Country Spanish website.

## Status Key
- [ ] Not started
- [x] Completed

---

## 1. Technical SEO Fundamentals

### robots.txt
- [x] Create `wwwroot/robots.txt` with basic crawl directives
- [x] Add sitemap reference

### XML Sitemap
- [x] Create `SitemapController.cs` for dynamic XML sitemap generation
- [x] Include all public pages (Home, About, Contact, Plans, etc.)
- [x] Set appropriate `lastmod` dates
- [x] Register route at `/sitemap.xml`

---

## 2. Meta Tags (_Layout.cshtml)

### Meta Descriptions
- [x] Add `<meta name="description">` tag with ViewData fallback
- [ ] Set unique descriptions per page via ViewData["MetaDescription"]

### Canonical URLs
- [x] Add `<link rel="canonical">` tag
- [x] Generate absolute URLs based on current request

### Page Titles
- [x] Verify unique `<title>` tags per page
- [x] Format: "Page Name | Lake Country Spanish"

---

## 3. Social Media / Open Graph Tags

### Open Graph (Facebook, LinkedIn)
- [x] Add `og:title` tag
- [x] Add `og:description` tag
- [x] Add `og:image` tag (1200x630px recommended)
- [x] Add `og:url` tag
- [x] Add `og:type` tag (website)
- [x] Add `og:site_name` tag
- [x] Add `og:image:width` and `og:image:height` tags

### Twitter Cards
- [x] Add `twitter:card` tag (summary_large_image)
- [x] Add `twitter:title` tag
- [x] Add `twitter:description` tag
- [x] Add `twitter:image` tag

---

## 4. Images

### Open Graph Image
- [x] Create OG image (1200x630px) for social sharing
- [x] Save to `wwwroot/img/og-image.jpg`
- [ ] Consider creating page-specific OG images for key pages

**Note:** The OG image should be created using design tools (Canva, Figma, etc.) with:
- Dimensions: 1200x630 pixels
- Include: Logo, tagline, and brand colors (navy #1e3a5f, coral #e85a42)
- Text: "Lake Country Spanish" + "Private Spanish Lessons with Karen"

---

## 5. Per-Page Meta Descriptions (TODO)

Add unique meta descriptions to key public pages:

- [ ] Home/Index: "Private Spanish lessons with Karen, a native Peruvian teacher..."
- [ ] Home/About: "Meet Karen - your personal Spanish tutor..."
- [ ] Contact/Index: "Get in touch to schedule your first Spanish lesson..."
- [ ] Subscription/Plans: "Choose from flexible Spanish lesson plans..."

---

## 6. Verification

- [ ] Test robots.txt at `/robots.txt`
- [ ] Test sitemap at `/sitemap.xml`
- [ ] Validate with Google Search Console
- [ ] Test Open Graph with Facebook Sharing Debugger
- [ ] Test Twitter Cards with Twitter Card Validator

---

## Implementation Notes

### ViewData Keys for Per-Page SEO
```csharp
ViewData["Title"] = "About Karen";
ViewData["MetaDescription"] = "Learn about Karen's background...";
ViewData["OgImage"] = "/img/about-og.jpg"; // Optional override
```

### Default Values in _Layout.cshtml
- Default description: "Personalized online Spanish lessons with Karen, a native Peruvian teacher..."
- Default OG image: `https://lakecountryspanish.com/img/og-image.jpg`

### Files Modified
- `wwwroot/robots.txt` - Created
- `Controllers/SitemapController.cs` - Created
- `Views/Shared/_Layout.cshtml` - Updated with canonical, OG, and Twitter tags
