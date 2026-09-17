# Session and performance update

## Root causes
- Activity was batched at one second, and normal foreground HTTP calls also renewed inactivity.
- The notification service requested dashboard/sla at login/root initialization, outside the SLA page.
- Routes statically imported all feature components.
- The monitoring endpoint existed, but there was no client connection or operational event publisher.
- Concurrent GET subscribers had no shared in-flight request.

## Focused changes
- Shared/SessionRepository.cs
- EnTrackBag.Api/Hubs/MonitoringHub.cs
- UI/src/app/services/auth.service.ts
- UI/src/app/services/monitoring.service.ts
- UI/src/app/services/notification.service.ts
- UI/src/app/core/auth.interceptor.ts
- UI/src/app/core/api.service.ts
- UI/src/app/app.routes.ts
- UI/src/app/app.component.ts
- UI/src/app/login/login.component.ts
- UI/src/app/dashboard/summary/summary.component.ts
- UI/src/app/dashboard/sla/sla.component.ts
- UI/src/app/device-status/device-status.component.ts
- UI/tests/session-management.test.cjs
- UI/tests/monitoring-performance.test.cjs

Activity uses trusted form input/change/submit, clicks on controls, and navigation. No scroll, wheel, mousemove or keydown listeners. Local activity updates immediately; server reports are at most once per 60 seconds with one request in flight. A bounded age header prevents the trailing report from recording the send time as the interaction time. Normal API calls, polling and SignalR do not renew activity. At 300 seconds Angular clears the session, stops the connection and redirects. Server validation/touch/expiry continue to reject expired sessions.

All eight page routes are lazy-loaded. Only the opened page loads operational APIs; login/root no longer fetch SLA. Concurrent identical GETs share a request per token; results are not retained as stale cross-page/cross-user cache.

The singleton monitoring client registers four handlers once, shares startup, resubscribes after reconnect and stops on logout/timeout. Page-scoped subscriptions are permission-checked at the hub. Summary, SLA/alarm, and device/system snapshots use existing domain components. The existing server watcher samples only the subscribed page every 15 seconds and sends changed snapshots; there is no new browser polling. Alarm counts derive from existing SLA breach records only while SLA is subscribed; no independent alarm source or new database table is invented. Operational data failures cannot manufacture updates.

## Actual schema / database
Read UserSessions columns and indexes from BLTSMFT before editing. Verified LastActivityAt datetime2 NOT NULL, LoginAt, LogoutAt, LogoutReason, IsActive, TokenExpiresAt and filtered unique index UX_UserSessions_OneActivePerUser.
Existing login transaction revokes previous active sessions and retains their rows. Existing active count excludes expired/logged-out/revoked sessions. Login/logout timestamps remain available for duration/history. No SQL mutation or schema change was performed. No EF migrations, EnsureCreated or Migrate.

## Verification
- node --test tests/session-management.test.cjs tests/monitoring-performance.test.cjs: 16 passed.
- Angular npm run build: passed, feature lazy chunks verified; initial development bundle 1.66 MB (previously 1.95 MB).
- dotnet build EnTrackBag.sln -c Release --no-restore -m:1 -p:BaseOutputPath=<workspace>/artifacts/session-performance/: passed, 0 errors, existing NU1510 warning.
- Scoped git diff --check: passed.
- Tests use deterministic Angular/HTTP/SignalR fakes. No new live browser/database end-to-end test was run; existing logged-in sessions were not disturbed. Restart/redeploy both rebuilt APIs and refresh Angular together to activate the server changes.
- Tag Report and Bag Journey remain existing page shells; no unrelated modules added.

## Diff
Session-Performance.diff is the scoped working-tree diff against HEAD, including prior uncommitted activity-service edits plus the two new source/test files. Other pending Add User/database/cache changes are not included.
No PR was created or merged.

