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
- `/`: already authenticated visitors are redirected to `/onboarding` or `/app`
  according to current-user state, as before.
- The landing page remains public when a current-user check fails, including during
  an API outage. Authorization is *still* performed independently by protected routes.
- Primary calls to action link to the existing `/register` flow.
- Returning-user links go to `/login`.
- The features/how-it-works/FAQ links are page anchors, not fake application routes.
- The visual dashboard is **illustrative data**, not a live, publicly accessible
  household or customer account. Its household/personal switch changes local
  presentation only.

## Implementation

- `apps/averio-web/src/features/landing/LandingPage.tsx`: responsive Tailwind
  layout, product preview, navigation, FAQ, CTA, theme and language controls.
- `apps/averio-web/src/routes/index.tsx`: homepage guard and public rendering.
- `apps/averio-web/src/i18n/locales/landing.ts` and `src/i18n/index.ts`:
  complete EN/DE/HU marketing translations.
- `apps/averio-web/index.html`: descriptive browser title and meta description.

Uses the existing `AverioBrand` and `ThemeProvider`. No backend endpoint, billing,
database migration, client generation or major dependency is required.

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
