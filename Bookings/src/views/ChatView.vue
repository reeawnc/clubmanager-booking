<template>
  <div class="chat-container">
    <!-- Clear button - floating in top right -->
    <button 
      v-if="messages.length > 0"
      @click="clearChat" 
      class="clear-button"
      title="Clear conversation"
    >
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M3 6h18"></path>
        <path d="M19 6v14c0 1-1 2-2 2H7c-1 0-2-1-2-2V6"></path>
        <path d="M8 6V4c0-1 1-2 2-2h4c1 0 2 1 2 2v2"></path>
      </svg>
    </button>

    <!-- Messages Area -->
    <main class="messages-container" ref="messagesContainer">
      <!-- Initial prompt buttons when no messages -->
      <div v-if="messages.length === 0" class="initial-prompts">
        <h2>Squash Court Assistant</h2>
        <div class="prompt-buttons">
          <button 
            v-for="prompt in quickPrompts" 
            :key="prompt.id"
            @click="sendQuickAction(prompt.text)"
            class="prompt-btn"
          >
            {{ prompt.text }}
          </button>
        </div>
      </div>

      <!-- Chat messages -->
      <div class="messages-list">
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
import { storeToRefs } from 'pinia'
import { useChatStore } from '@/stores/chat'
import ChatMessage from '@/components/ChatMessage.vue'

const chatStore = useChatStore()
const { messages, isLoading } = storeToRefs(chatStore)
const { sendMessage: storeSendMessage, clearChat } = chatStore

const inputMessage = ref('')
const messageInput = ref<HTMLTextAreaElement>()
const messagesContainer = ref<HTMLElement>()

const quickPrompts = [
  {
    id: 1,
    text: "Show me the court timetable for today after 5pm"
  },
  {
    id: 2,
    text: "Whats availalbe for today after 5pm?"
  },
  {
    id: 3,
    text: "Show me today's court timetable"
  },
  {
    id: 4,
    text: "What time slots are available tomorrow after 5pm?"
  },
  {
    id: 5,
    text: "What time slots are availalbe on this day next week after 5pm?"
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
  // On mobile, allow Enter to create new lines, use Shift+Enter to send
  // On desktop, use Enter to send, Shift+Enter for new lines
  const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)
  
  if (isMobile) {
    // Mobile: Enter = new line, Shift+Enter = send
    if (event.key === 'Enter' && event.shiftKey) {
      event.preventDefault()
      sendMessage()
    }
  } else {
    // Desktop: Enter = send, Shift+Enter = new line
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      sendMessage()
    }
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
  background: #0d1117; /* Darker like cursor theme */
  color: #e6edf3;
}

/* Clear button - floating in top right */
.clear-button {
  position: fixed;
  top: 1rem;
  right: 1rem;
  z-index: 100;
  background: #21262d;
  border: 1px solid #30363d;
  color: #7d8590;
  padding: 0.75rem;
  border-radius: 50%;
  cursor: pointer;
  transition: all 0.2s ease;
  width: 44px;
  height: 44px;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(8px);
}

.clear-button:hover {
  background: #30363d;
  color: #e6edf3;
  border-color: #58a6ff;
  transform: translateY(-1px);
  box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4);
}

.clear-button:active {
  transform: translateY(0);
}

.messages-container {
  flex: 1;
  overflow-y: auto;
  padding: 0.5rem;
}

/* Initial prompts styling */
.initial-prompts {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  padding: 2rem;
  height: 100%;
}

.initial-prompts h2 {
  color: #e6edf3;
  font-size: 2rem;
  margin-bottom: 0.5rem;
  font-weight: 600;
}

.initial-prompts p {
  color: #7d8590;
  font-size: 1.1rem;
  margin-bottom: 2rem;
}

.prompt-buttons {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  width: 100%;
  max-width: 500px;
}

.prompt-btn {
  background: #21262d;
  border: 1px solid #30363d;
  color: #e6edf3;
  padding: 1rem 1.5rem;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.95rem;
  text-align: left;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.prompt-btn:hover {
  background: #30363d;
  border-color: #58a6ff;
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(88, 166, 255, 0.15);
}

.prompt-btn:active {
  transform: translateY(0);
}

/* Welcome section styles removed since welcome was removed */

.messages-list {
  max-width: 768px;
  margin: 0 auto;
  padding: 2rem 0;
}

.input-section {
  border-top: 1px solid #21262d;
  padding: 0.75rem 0.5rem;
  background: #161b22; /* Darker like cursor theme */
  /* Ensure input stays above mobile browser UI */
  padding-bottom: max(0.75rem, env(safe-area-inset-bottom));
}

.input-container {
  width: 100%;
  margin: 0;
}

.input-wrapper {
  display: flex;
  align-items: flex-end;
  gap: 0.5rem;
  background: #21262d; /* Darker like cursor theme */
  border: 1px solid #30363d;
  border-radius: 0.75rem;
  padding: 0.75rem;
  transition: border-color 0.2s;
  min-height: 48px; /* Ensure minimum height for mobile touch targets */
}

.input-wrapper:focus-within {
  border-color: #58a6ff; /* Blue accent like cursor theme */
}

.message-input {
  flex: 1;
  background: none;
  border: none;
  color: #e6edf3; /* Cursor theme text color */
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
  color: #7d8590; /* Cursor theme placeholder color */
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
  background: #0d1117;
}

.messages-container::-webkit-scrollbar-thumb {
  background: #30363d;
  border-radius: 3px;
}

.messages-container::-webkit-scrollbar-thumb:hover {
  background: #484f58;
}
</style>