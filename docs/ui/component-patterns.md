# Averio UI component patterns

**Status:** Accepted implementation guidance  
**Updated:** 2026-09-25

These patterns record the agreed transfer UI refinements and apply to new or changed empty states and cards across Averio. They extend the canonical HTML concept. Existing screens can migrate when touched; this is not a requirement to restyle the whole application at once.

## Illustrated empty states

Use a lightweight, responsive Averio illustration above a short localized heading, helpful explanation, and one contextual primary action. Center the composition with comfortable spacing and a constrained text width. Support light and dark themes with Tailwind utilities.

- With no filters and no visible records, offer the feature's create action. Transfers use “Your money, on the move”, an explanation, and “Add transfer”. Respect the same permission/loading prerequisites as the normal create control.
- With active filters and no matches, explain that nothing matches and offer “Reset filters”, clearing all applicable criteria. Do not claim the household has no records: the result is limited to what the current user may see.
- Render an empty state only after a successful, valid query. Loading, request failures, and invalid filters retain their own feedback.
- Localize headings, explanations, and actions in English, German, and Hungarian. Keep copy and callbacks in the feature, not in the illustration.
- Use repository-native SVG components for simple illustrations, styled with Tailwind. Reuse the official `AverioLogo` rather than copying its paths or inventing a replacement logo. Artwork should be decorative (`aria-hidden`) when the adjacent text conveys its meaning, with no focusable elements or required motion.

Reference: `features/transfers/TransfersPage.tsx` and `shared/components/ui/AverioTransferIllustration.tsx`. The transfer illustration uses two wallets, an emerald arrow, and the official leaf mark. Other features should use relevant artwork while retaining this composition and behavior.

## Menu-inspired cards

Cards follow the selected navigation item's visual language: rounded corners, a subtle tinted background, a thin border, and a narrow emerald left accent. Use `shared/components/ui/AverioCard` for reusable content/form groups, with native props and refs. Its Tailwind surface uses a 17px radius, 3px primary-color left border, light background `#F2FAF7`, and dark background `#112B36`.

One logical item must read as one card. Related account sides, amounts, status, notes, and actions share an outer surface; do not split them into disconnected floating cards. Use spacing or a subtle internal divider for a footer. Keep headings and primary values readable in both themes. A noninteractive card must not imply it is clickable; use explicit buttons for actions. Voided or destructive states retain their status text and appropriate neutral/red cues rather than relying only on the emerald accent.

Create/edit dialogs follow the same treatment. Group related controls into cards with localized headings and accessible names, retaining labels, validation, required/disabled controls, native form submission, and logical keyboard order. Stack groups on small screens; use columns only when controls fit without clipping. Keep dialog actions in the shared dialog footer and destructive confirmation visually distinct.

Reference: the transfer overview's unified cards and the transfer editor's “Accounts and amounts” and “Transfer details” groups. Use the same grouping for create and update; account immutability and financial behavior must not change as part of presentation work.


## Category and icon choices

Use `AverioCategorySelect` for every category choice, including parent selection and transaction items. Filter eligibility first; sort personal pins first and displayed names alphabetically using the current UI locale. Include parent context for children. Category overviews keep alphabetical trees and a pinned shortcut section. Use the shared `categoryOrdering` helper rather than seed order or untranslated keys.

Use `AverioIconPicker` for icon selection: localized named tiles, name search, current selection preview, native radio keyboard behavior and clear focus states. Preserve saved icons missing from the current catalog. New catalog entries need meaningful API names and EN/DE/HU translations.
