# Court Booking Assistant

A sleek, ChatGPT-like Vue.js interface for interacting with your Azure Function App court booking system.

## Features

- 🌙 Dark theme inspired by ChatGPT
- 💬 Real-time chat interface
- ⚡ Quick action buttons for common queries
- 🎾 Court availability and booking information
- 📱 Responsive design

## Setup

1. Install dependencies:
```bash
npm install
```

2. Start the development server:
```bash
npm run dev
```

3. Make sure your Azure Function App is running locally on `http://localhost:7071`

## API Integration

The app connects to your Azure Function at:
- **Endpoint**: `http://localhost:7071/api/PromptFunction`
- **Method**: POST
- **Payload**:
```json
{
    "prompt": "Show me the court availability for today",
    "userId": "testuser123", 
    "sessionId": "testsession456"
}
```

## Quick Actions

The interface includes preset buttons for common queries:
- "Show me court availability for today after 5pm"
- "Who's playing today after 5pm?"
- "Show me today's court availability"

## Tech Stack

- Vue 3 with TypeScript
- Pinia for state management
- Vue Router
- Vite for build tooling

## Development

- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint
- `npm run format` - Format code with Prettier