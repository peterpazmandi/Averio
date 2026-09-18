# ADR-008 — Cookie-based web authentication

**Status:** Accepted  
**Date:** 2026-09

## Context

Averio is primarily a first-party web application. Storing long-lived bearer tokens in browser-accessible storage creates avoidable security risk.

## Decision

Use secure cookie-based authentication for the web application.

Core flows include registration, email verification, resend verification, login, logout, forgot password, and reset password.

Cookie, CORS, and CSRF configuration must be designed together. Production cookies are secure and credentials are allowed only from explicit trusted frontend origins.

## Consequences

- frontend requests use credentials where required
- no auth token in localStorage as the normal auth design
- server owns session/auth validation
- anonymous auth/email endpoints need rate limiting and enumeration-safe behavior
