# Bookings API Eval Tests

How to run:

- Ensure `OpenAI_API_Key` is set in your environment (tests will skip if missing)
- From repo root:
  - `dotnet test Bookings/tests`

What these tests do:
- CourtAvailabilityFormattingTests: fetches live JSON from the tool and asserts structural sufficiency for downstream LLM formatting/filtering (e.g., by court/time) without being brittle to wording.
- CourtAvailabilityAgentTests: currently skipped examples that demonstrate stricter formatting checks; re-enable after we standardize the agent to always emit canonical sections for targeted queries.
- Other agent tests are temporarily removed per request to focus on court availability first.

Notes:
- Tests use soft assertions (regex/contains) and validate structure/invariants rather than exact phrasing.
- You can extend by pinning a snapshot (golden sample) of the tool JSON and using it to test LLM-only formatting in isolation.
