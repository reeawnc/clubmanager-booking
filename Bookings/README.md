# ClubManager Booking — Frontend (Vue) + API (Azure Functions)

A chat-driven squash court assistant with a Vue 3 frontend and an Azure Functions API (multi-agent, OpenAI tools, and Azurite-backed storage for assistants and files).

## How to run locally (Windows)

Open three terminals and run these commands in order:

1) Storage emulator (Azurite)
```bash
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

2) API (Azure Functions)
```bash
cd Bookings/api
func start
```
Default URL: `http://localhost:7071`

3) Frontend (Vue)
```bash
cd Bookings
npm ci
npm run dev
```
Default URL: `http://localhost:5173`

### Prerequisites
- Node 18+
- .NET 8 SDK
- Azure Functions Core Tools v4
- Azurite (for local Azure Storage emulation)
- OpenAI API key configured in `Bookings/api/local.settings.json` as `OpenAI_API_Key` (used by agents/tools)

## Project structure
- `Bookings/` – Vue frontend
- `Bookings/api/` – Azure Functions (isolated worker)
  - Agents: `Agents/`
  - Tools: `Tools/`
  - Services: `Services/`
  - Functions: `PromptFunction.cs` (main), plus test-only functions under project root

## Primary app flow
- Frontend posts to `POST /api/PromptFunction` with:
```json
{ "prompt": "...", "userId": "...", "sessionId": "..." }
```
- The API routes the prompt via `PrimaryAgent` to sub-agents based on keywords or LLM routing.
- Agents can call domain-specific tools to fetch/act on real data.

## Agents (multi-agent architecture)
- `CourtAvailabilityAgent`: checks court day schedule and formats availability.
- `BookingAgent`: books courts (prefers Court 1 → 2 → 3) via tools.
- `CancellationAgent`: placeholder conversational cancellation.
- `BoxResultsAgent`: OpenAI Assistants + File Search over aggregated box results.
- `MyBookingsAgent`: summarizes current user bookings.
- `MessagesAgent`: inbox/unread/sent messages summary.
- `BoxPositionsAgent`: current box league positions.

## Tools (selected)
- `get_court_availability`, `book_court`
- `get_box_results` (OpenAI file search), persistence via Azure Blob
- `get_my_bookings`
- `get_user_has_messages`, `get_user_messages`, `get_sent_user_messages`
- `cancel_court`
- `get_box_positions` (accepts `groupId` or `group` enum: `Club`, `SummerFriendlies`)

## Test-only API endpoints (service isolation)
These bypass agents so you can validate integrations one by one.

- `POST /api/test/my-bookings`
  - No body

- `POST /api/test/messages/has`
  - No body

- `POST /api/test/messages/inbox`
  - Body (optional):
  ```json
  { "markAsRead": false, "showExpired": false, "showRead": true }
  ```

- `POST /api/test/messages/sent`
  - No body

- `POST /api/test/cancel-court`
  - Body:
  ```json
  { "bookingId": 12345 }
  ```

- `POST /api/test/box-positions`
  - Body (either):
  ```json
  { "groupId": "216" }
  ```
  or
  ```json
  { "groupId": "418" }
  ```
  or using enum-like name via chat prompt (through agent): `Club`, `SummerFriendlies`

## Configuration
- `Bookings/api/local.settings.json`
  - `AzureWebJobsStorage`: `UseDevelopmentStorage=true` (works with Azurite)
  - `OpenAI_API_Key`: required for LLM agents/tools and file search

## Quick prompts (frontend)
Initial buttons in the chat showcase key features:
- Court availability: “Show me the court timetable for today after 5pm”
- Booking: “Book a court for 18:00 today”
- My bookings: “Show my bookings”
- Messages: “Do I have any unread messages?”, “Show my inbox messages”, “Show my sent messages”
- Box positions: “Show current box positions for Club/SummerFriendlies”
- Box results (RAG): results summaries and comparisons

## Deploy (Azure Static Web Apps)
The repo includes a workflow that builds the Vue app and deploys an integrated API from `Bookings/api`. Ensure the same settings (`AzureWebJobsStorage`, `OpenAI_API_Key`) are configured in the Azure environment.