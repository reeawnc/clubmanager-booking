<template>
  <div class="chat-container">
    <!-- Header -->
    <header class="chat-header">
      <div class="header-content">
        <h1 class="chat-title">Court Booking Assistant</h1>
        <div class="header-buttons">
          <button 
            @click="testConnection" 
            class="test-btn"
            title="Test Azure Function connection"
          >
            Test API
          </button>
          <button 
            @click="clearChat" 
            class="clear-btn"
            title="Clear conversation"
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M3 6h18"></path>
              <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"></path>
              <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"></path>
            </svg>
          </button>
        </div>
      </div>
    </header>

    <!-- Messages Area -->
    <main class="messages-container" ref="messagesContainer">
      <!-- Welcome message when no messages -->
      <div v-if="messages.length === 0" class="welcome-section">
        <div class="welcome-content">
          <h2 class="welcome-title">Welcome to Court Booking Assistant</h2>
          <p class="welcome-subtitle">Ask me about court availability, bookings, or who's playing today!</p>
          
          <!-- Quick action buttons -->
          <div class="quick-actions">
            <button 
              v-for="action in quickActions" 
              :key="action.id"
              @click="sendQuickAction(action.prompt)"
              class="quick-action-btn"
            >
              {{ action.label }}
            </button>
          </div>
        </div>
      </div>

      <!-- Chat messages -->
      <div v-else class="messages-list">
        <ChatMessage 
          v-for="message in messages" 
          :key="message.id"
          :message="message"
        />
      </div>
    </main>

    <!-- Input Area -->
    <footer class="input-section">
      <div class="input-container">
        <div class="input-wrapper">
          <textarea
            v-model="inputMessage"
            @keydown="handleKeyDown"
            @input="autoResize"
            placeholder="Ask about court availability, bookings, or players..."
            class="message-input"
            rows="1"
            ref="messageInput"
            :disabled="isLoading"
          ></textarea>
          <button 
            @click="sendMessage"
            :disabled="!inputMessage.trim() || isLoading"
            class="send-btn"
          >
            <svg v-if="!isLoading" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="22" y1="2" x2="11" y2="13"></line>
              <polygon points="22,2 15,22 11,13 2,9 22,2"></polygon>
            </svg>
            <div v-else class="loading-spinner"></div>
          </button>
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted } from 'vue'
import { useChatStore } from '@/stores/chat'
import ChatMessage from '@/components/ChatMessage.vue'

const chatStore = useChatStore()
const { messages, isLoading, sendMessage: storeSendMessage, clearChat } = chatStore

const inputMessage = ref('')
const messageInput = ref<HTMLTextAreaElement>()
const messagesContainer = ref<HTMLElement>()

const quickActions = [
  {
    id: 1,
    label: "🎾 Show me court availability for today after 5pm",
    prompt: "Show me the court availability for today after 5pm"
  },
  {
    id: 2,
    label: "👥 Who's playing today after 5pm?",
    prompt: "whos playing today after 5pm"
  },
  {
    id: 3,
    label: "📅 Show me today's court availability",
    prompt: "Show me the court availability for today"
  },
  {
    id: 4,
    label: "🕰️ What time slots are available tomorrow?",
    prompt: "What time slots are available tomorrow?"
  },
  {
    id: 5,
    label: "🏋️ Are there any training sessions today?",
    prompt: "Are there any training sessions scheduled for today?"
  }
]

const sendMessage = async () => {
  if (!inputMessage.value.trim() || isLoading.value) return
  
  const message = inputMessage.value.trim()
  inputMessage.value = ''
  
  await storeSendMessage(message)
  await scrollToBottom()
}

const sendQuickAction = async (prompt: string) => {
  await storeSendMessage(prompt)
  await scrollToBottom()
}

const testConnection = async () => {
  try {
    // With Azure Static Web Apps integrated API, always use /api
    const response = await fetch('/api/PromptFunction', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        prompt: 'test connection',
        userId: 'testuser123',
        sessionId: 'testsession456'
      }),
    })
    
    if (response.ok) {
      const data = await response.json()
      alert('✅ Connection successful! Your Azure Function is responding correctly.')
    } else {
      const errorText = await response.text()
      alert(`❌ Connection failed with status ${response.status}. Please check that your Azure Function is running on localhost:7071.`)
    }
  } catch (error) {
    alert(`❌ Connection error: ${error instanceof Error ? error.message : 'Unknown error'}. Make sure your Azure Function is running.`)
  }
}

const handleKeyDown = (event: KeyboardEvent) => {
  if (event.key === 'Enter' && !event.shiftKey) {
    event.preventDefault()
    sendMessage()
  }
}

const autoResize = () => {
  if (messageInput.value) {
    messageInput.value.style.height = 'auto'
    messageInput.value.style.height = Math.min(messageInput.value.scrollHeight, 120) + 'px'
  }
}

const scrollToBottom = async () => {
  await nextTick()
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

onMounted(() => {
  messageInput.value?.focus()
})
</script>

<style scoped>
.chat-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  height: 100dvh; /* Use dynamic viewport height for mobile */
  background: #1a1a1a;
  color: #e5e5e5;
}

.chat-header {
  border-bottom: 1px solid #333;
  padding: 1rem 1rem;
  background: #212121;
}

@media (min-width: 640px) {
  .chat-header {
    padding: 1rem 2rem;
  }
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: center;
  max-width: 768px;
  margin: 0 auto;
}

.header-buttons {
  display: flex;
  gap: 0.5rem;
}

.chat-title {
  font-size: 1.125rem;
  font-weight: 600;
  color: #fff;
}

@media (min-width: 640px) {
  .chat-title {
    font-size: 1.25rem;
  }
}

.test-btn,
.clear-btn {
  background: none;
  border: 1px solid #444;
  color: #888;
  padding: 0.5rem;
  border-radius: 0.375rem;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 0.875rem;
}

.test-btn {
  padding: 0.5rem 0.75rem;
}

.test-btn:hover,
.clear-btn:hover {
  background: #333;
  color: #fff;
  border-color: #555;
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 0 1rem;
}

@media (min-width: 640px) {
  .messages-container {
    padding: 0 2rem;
  }
}

.welcome-section {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
  max-width: 768px;
  margin: 0 auto;
}

.welcome-content {
  text-align: center;
  padding: 2rem;
}

.welcome-title {
  font-size: 2rem;
  font-weight: 600;
  color: #fff;
  margin-bottom: 0.5rem;
}

.welcome-subtitle {
  font-size: 1.1rem;
  color: #888;
  margin-bottom: 2rem;
}

.quick-actions {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.75rem;
  max-width: 500px;
  margin: 0 auto;
}

@media (min-width: 640px) {
  .quick-actions {
    grid-template-columns: 1fr 1fr;
  }
}

.quick-action-btn {
  background: #2a2a2a;
  border: 1px solid #444;
  color: #e5e5e5;
  padding: 0.875rem 1.5rem;
  border-radius: 0.5rem;
  cursor: pointer;
  transition: all 0.2s;
  text-align: left;
  font-size: 0.9rem;
}

.quick-action-btn:hover {
  background: #333;
  border-color: #555;
  transform: translateY(-1px);
}

.messages-list {
  max-width: 768px;
  margin: 0 auto;
  padding: 2rem 0;
}

.input-section {
  border-top: 1px solid #333;
  padding: 1rem 1rem;
  background: #212121;
  /* Ensure input stays above mobile browser UI */
  padding-bottom: max(1rem, env(safe-area-inset-bottom));
}

@media (min-width: 640px) {
  .input-section {
    padding: 1.5rem 2rem;
  }
}

.input-container {
  max-width: 768px;
  margin: 0 auto;
}

.input-wrapper {
  display: flex;
  align-items: flex-end;
  gap: 0.5rem;
  background: #2a2a2a;
  border: 1px solid #444;
  border-radius: 0.75rem;
  padding: 0.75rem;
  transition: border-color 0.2s;
  min-height: 48px; /* Ensure minimum height for mobile touch targets */
}

@media (min-width: 640px) {
  .input-wrapper {
    gap: 0.75rem;
  }
}

.input-wrapper:focus-within {
  border-color: #555;
}

.message-input {
  flex: 1;
  background: none;
  border: none;
  color: #e5e5e5;
  resize: none;
  outline: none;
  font-family: inherit;
  font-size: 1rem;
  line-height: 1.5;
  max-height: 120px;
  overflow-y: auto;
  min-height: 20px; /* Ensure minimum height */
  padding: 0.25rem 0; /* Add some padding for better touch */
}

.message-input::placeholder {
  color: #666;
}

.send-btn {
  background: #10a37f;
  border: none;
  color: white;
  padding: 0.625rem;
  border-radius: 0.375rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: background-color 0.2s;
  min-width: 44px; /* Larger touch target for mobile */
  height: 44px; /* Larger touch target for mobile */
  flex-shrink: 0; /* Prevent button from shrinking */
}

.send-btn:hover:not(:disabled) {
  background: #0d8b6b;
}

.send-btn:disabled {
  background: #444;
  cursor: not-allowed;
}

.loading-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid #666;
  border-top: 2px solid #fff;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* Scrollbar styling */
.messages-container::-webkit-scrollbar {
  width: 6px;
}

.messages-container::-webkit-scrollbar-track {
  background: #1a1a1a;
}

.messages-container::-webkit-scrollbar-thumb {
  background: #444;
  border-radius: 3px;
}

.messages-container::-webkit-scrollbar-thumb:hover {
  background: #555;
}
</style>