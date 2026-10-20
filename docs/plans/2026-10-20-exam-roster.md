# Exam roster implementation

Implement Long's L1-L4 against the existing IExamRosterService contract. Reuse the
MVC layout, ExamAccessGuard, SQL constraints and IExamUnitOfWork. No migration,
account creation or spreadsheet import is required.

1. Implement and test add/remove/list against SQL Server. Normalize email, require
   an active nondeleted STUDENT, reject duplicates, enforce ownership and Draft,
   and prevent removal of enrollments with sessions. Serialize mutations on the
   exam row and update its version so schedule previews can detect roster changes.
2. Add MVC forms and a roster entry page listing managed exams. Preserve validation
   errors, use antiforgery on every mutation, and display Vietnam slot times.
3. Verify concurrent requests, rollback, notifications and browser HTTP routes;
   run the existing suite and document integration contracts and remaining demo work.

Commits: service/persistence with tests; MVC with HTTP tests; integration evidence
and handoff. Comments explain authorization, locking and commit boundaries.
Keep the shared DI changes minimal and document them for Kiet. Extend RosterEntry
with an optional Email parameter so existing positional constructors still work.
