# Public landing page

**Status:** Implemented on `feat/public-landing-page` for review  
**Design source:** the approved Averio landing-page HTML concept and `docs/ui/averio-ui-concept.html`

## Goal and positioning

Averio's first public page is a product landing page rather than an authentication form.
It explains why household-first finances with personal privacy are different from
traditional individual-only budgeting tools. The primary conversion event is a visitor
choosing to register; the secondary event is a visitor returning to sign in.

The value proposition is **"Your money. Together, on your terms."** The page leads with:
shared household context, member autonomy, useful financial reporting, and clear
onboarding. It avoids artificial urgency, fabricated reviews/customer metrics and
unannounced subscription pricing.

## Routing and behavior

- `/`: anonymous visitors see the landing page.
- `/`: authenticated visitors are redirected to `/onboarding` or `/app` as soon as
  the shared auth provider confirms their session. The redirect replaces history.
- Public content renders immediately while the session request is pending and
  remains available when it fails, including during an API outage. Authorization
  is still performed independently by protected routes.
- Primary calls to action link to the existing `/register` flow.
- Returning-user links go to `/login`.
- The features/how-it-works/FAQ links are page anchors, not fake application routes.
- The visual dashboard is **illustrative data**, not a live, publicly accessible
  household or customer account. Its household/personal switch changes local
  presentation only.

## Implementation

- `apps/averio-web/src/features/landing/LandingPage.tsx`: responsive Tailwind
  layout, product preview, navigation, FAQ, CTA, theme and language controls.
- `apps/averio-web/src/routes/index.tsx`: public rendering and reactive authenticated redirect.
- `apps/averio-web/src/i18n/locales/landing.ts` and `src/i18n/index.ts`:
  complete EN/DE/HU marketing translations.
- `apps/averio-web/index.html`: descriptive browser title and meta description.

Uses the existing `AverioBrand` and `ThemeProvider`. No backend endpoint, billing,
database migration, client generation or major dependency is required.

The landing and document wrappers use horizontal clipping rather than a hidden
overflow scroll container so the header remains sticky to the viewport. Primary
CTAs and selected preview buttons use white text on `primary-700` (about 5.48:1
contrast), with `primary-800` hover backgrounds for registration links.

## Automated verification

- `apps/averio-web/tests/landing-page.test.mjs` covers immediate public rendering,
  auth state changes, onboarding redirects, history replacement and stale data.
- `tests/collaboration/refresh.mjs` verifies signed-out `/app` redirects separately
  from public `/`, retaining signed-in cold-load/reload checks.
- `tests/collaboration/landing.mjs` runs in production and development StrictMode:
  held/503 session responses, sticky navigation, 320px/desktop EN/DE/HU in both
  themes, CTA contrast, preview switching, mobile anchors and FAQ expansion.

## Feature claims and launch boundaries

The page features implemented areas: account management, transactions, transfers,
recurring income/expenses, category management, activities, household invitations,
personal-money functionality and statistics. It describes EUR/USD/HUF as *separate*
balances; it does not imply automatic FX conversion.

Do **not** present these as available until they ship: bank connections, AI receipt
scanning, CSV import, goals or social login. The FAQ explicitly distinguishes future
bank integrations. Pricing is not fabricated; legal/privacy/terms content needs
review before launch.

## Manual verification checklist

1. Open `/` without a session: landing page is visible and links work.
2. Open `/` with a valid session: navigate to dashboard or onboarding.
3. Make the API unavailable and open `/`: anonymous marketing page is still
   accessible; protected routes stay protected.
4. Test 320px mobile, tablet and wide desktop: no horizontal overflow;
   mobile navigation opens and closes; keyboard focus is visible.
5. Switch EN/DE/HU and light/dark themes and reload: preferences remain stable.
6. Switch demo household/personal views; confirm the figures remain labeled
   illustrative and never require a backend request.
7. Follow all registration/login CTAs; confirm existing flows still work.
8. Run `npm run build` and `npm run lint` in `apps/averio-web`.
9. Verify SPA fallback routing after deploying to Cloudflare.

## Conversion measurement (follow-up)

Track *aggregate* landing views, clicks to registration, registration completions
and first-account onboarding completions. Choose a consent-aware analytics setup
before adding trackers; do not send account balances, identities, or transaction
information to a marketing analytics service.

## SEO follow-up

The static HTML includes basic metadata, but full social-preview metadata,
canonical URLs, crawlable prerendering/SSR, sitemap and localized SEO metadata
can be added as part of a separate production launch pass.


## PR #20 review updates (2026-10-24)

### Shared preferences

The application, authentication and onboarding screens use `ConceptPreferences`.
The older `LanguageSwitcher` and `ThemeToggle` exports are not the active application controls.
The public header now uses `ConceptPreferences` too. Its wrapper and controls were migrated
from legacy CSS/inline styles to Tailwind, retaining the original 66px language selector,
38px theme button, 11px corners, existing SVG icon, accessible labels, i18next language
selection and ThemeProvider persistence. The select reuses `AverioSelect`. No unrelated
application styling was migrated and no package versions changed.

### Responsive hierarchy and preview

The hero scales from 32px to a maximum of 56px instead of 51–82px. Its final two phrases
flow together with balanced text rather than forced line breaks. The EN/DE/HU wording
retains the original hierarchy. The sample sidebar now reserves 144px; each icon/label
uses flex, a fixed icon slot, an explicit gap and no wrapping. Below 640px the sidebar
stays hidden to keep the useful preview readable. The public navigation collapses below
1280px so longer translated links and preferences fit; below 380px the official logo
remains visible while the header wordmark is hidden.

### Inclusive positioning

EN/DE/HU benefit copy, onboarding introduction and privacy question now address individuals,
couples, families and larger households. Invitations remain described as household membership,
without promises about unlimited seats, child accounts, permissions or an unannounced plan.
This broadens the audience without inventing features or pricing.

### Contact and navigation

`/contact` is a public TanStack route, including direct visits and reloads. It shares the
landing header/footer and supports all three languages and themes. Desktop/mobile navigation,
FAQ and footer link to it. The page provides a visible, copyable `support@averioapp.com`
mailto address, helpful support-request guidance, privacy guidance and return/FAQ links.
There is no form, submission API, fabricated support availability or response-time promise.
Email delivery and the actual support mailbox must be verified operationally before launch.

### Changed files and purposes

- `src/shared/components/concept/ConceptPreferences.tsx`: replace styling in the shared
  component; preserve behavior for every existing consumer.
- `src/features/landing/PublicLayout.tsx`: new shared public header/footer; prevents public
  pages from drifting apart and keeps mobile navigation consistent.
- `src/features/landing/LandingPage.tsx`: replace custom preferences/header/footer; adjust
  hero and preview layout; route the FAQ contact action to the contact page.
- `src/features/landing/ContactPage.tsx`: new localized support page using `AverioCard`.
- `src/routes/contact.tsx` and `src/routeTree.gen.ts`: register the public route.
- `src/i18n/locales/landing.ts`: update audience copy and add complete contact translations;
  remove obsolete landing-only preference labels.
- `tests/landing-review.browser.mjs`: production browser matrix and navigation/persistence
  regression coverage with screenshots. Run from `apps/averio-web` after `npm run build`;
  it starts its own preview. Set `LANDING_URL` to test an already running dev server.
- `tests/collaboration/landing.mjs` (repository root): update the public features anchor selector.
- `.github/workflows/verify.yml`: run the new browser suite and upload its screenshots.

All `src/` and standalone browser-test paths above are relative to `apps/averio-web`.
Complete resulting sources are committed to the existing PR branch; no separate PR is created.

### Visual references

The four original conversation attachments were retrieved and visually inspected:
`image(20261024-200737).png` (English hero/preferences),
`image(20261024-201045).png` (Hungarian oversized hero/sidebar),
`image(20261024-201135).png` (couple-only pillar), and
`image(20261024-201436).png` (FAQ contact action).
They are before-state references, not pixel-identical golden files: the requested fixes
intentionally change their geometry and copy. New screenshots are saved under
`test-results/landing-review/` and uploaded by CI.

### Verification results for this update

- Production build and ESLint passed. All 62 existing frontend regression tests passed.
- Development StrictMode and production Chromium runs each passed 36 combinations:
  320, 390, 768, 1024, 1440 and 1920px × EN/DE/HU × light/dark, on both public routes.
- Checked content bounds (not just document overflow), hero font cap, unwrapped sidebar
  rows, interactive preview, contact navigation, mailto target and absence of a form.
- Verified language/theme persistence after reload; production checks also covered
  navigation/FAQ/footer contact entry points, FAQ expansion, return to FAQ, login and
  registration routes, and equal shared-control dimensions between login and contact.
- Inspected new desktop Hungarian hero/contact and mobile screenshots against the
  supplied references. The Hungarian hero now fits three balanced lines at 1440px.
- Local browser tests mock anonymous/API-failure responses. They do not certify a live
  backend, actual email delivery, deployed SPA fallback or non-Chromium browsers. The
  full backend/integration pipeline remains the responsibility of the PR Verify workflow.
