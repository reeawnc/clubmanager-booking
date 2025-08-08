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
        <h4>AI Squash Assistant</h4>
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
            placeholder="Ask about courts..."
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
              <path d="M12 19V5M5 12l7-7 7 7"></path>
            </svg>
            <div v-else class="loading-spinner"></div>
          </button>
        </div>
      </div>
    </footer>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick, onMounted, onUnmounted } from 'vue'
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
  // Court availability
  { id: 1,  text: "Show me the court timetable for today after 5pm" },
  { id: 2,  text: "What time slots are available tomorrow after 5pm?" },
  { id: 3,  text: "Who's playing on Court 2 at 19:00 today?" },

  // Booking
  { id: 4,  text: "Book a court for 18:00 today" },
  { id: 5,  text: "Book a court for 18:45 this day next week" },

  // My bookings
  { id: 6,  text: "Show my bookings" },

  // Messages
  { id: 7,  text: "Do I have any unread messages?" },
  { id: 8,  text: "Show my inbox messages" },
  { id: 9,  text: "Show my sent messages" },

  // Box positions (use enums for clarity/context)
  { id: 10, text: "Show current box positions for Club" },
  { id: 11, text: "Show current box positions for SummerFriendlies" },

  // Box results (RAG)
  // Historical results via file search (spans years)
  { id: 12, text: `Historical results (file): Summarize box results for R Cunniffe across ${new Date().getFullYear()}` },
  { id: 13, text: `Historical results (file): Compare R Cunniffe vs Manolo Demery across ${new Date().getFullYear()}` },

  // Live results direct from ClubManager (current league only)
  { id: 14, text: "Live results: Show current SummerFriendlies match results" },
  { id: 15, text: "Live results: Show current Club match results" },
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

// Handle mobile keyboard
const handleResize = () => {
  // Force scroll to bottom when keyboard appears/disappears
  setTimeout(() => {
    scrollToBottom()
  }, 100)
}

onMounted(() => {
  messageInput.value?.focus()
  
  // Prevent page scrolling on mobile
  document.body.style.overflow = 'hidden'
  document.body.style.position = 'fixed'
  document.body.style.width = '100%'
  document.body.style.height = '100%'
  
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
  document.body.style.overflow = ''
  document.body.style.position = ''
  document.body.style.width = ''
  document.body.style.height = ''
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
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  overflow: hidden;
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
  -webkit-overflow-scrolling: touch;
  overscroll-behavior: contain;
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

.initial-prompts h4 {
  color: #e6edf3;
  font-size: 1rem;
  margin-bottom: 1rem;
  font-weight: 500;
  letter-spacing: 0.025em;
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
  padding: 0.75rem 1rem;
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.85rem;
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
  position: sticky;
  bottom: 0;
  z-index: 10;
}

.input-container {
  width: 100%;
  margin: 0;
}

.input-wrapper {
  display: flex;
  align-items: flex-end;
  gap: 0.5rem;
  background: #2f2f2f;
  border: 1px solid #565869;
  border-radius: 1.5rem;
  padding: 0.75rem;
  transition: all 0.2s ease;
  min-height: 48px;
}

.input-wrapper:focus-within {
  border-color: #8e8ea0;
}

.message-input {
  flex: 1;
  background: none;
  border: none;
  color: #ffffff;
  resize: none;
  outline: none;
  font-family: inherit;
  font-size: 1rem;
  line-height: 1.4;
  max-height: 120px;
  overflow-y: auto;
  min-height: 24px;
  padding: 2px 0;
  vertical-align: middle;
}

.message-input::placeholder {
  color: #8e8ea0;
}

.send-btn {
  background: #565869;
  border: none;
  color: #8e8ea0;
  padding: 0.5rem;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  width: 32px;
  height: 32px;
  flex-shrink: 0;
}

.send-btn:not(:disabled) {
  background: #ffffff;
  color: #2f2f2f;
}

.send-btn:hover:not(:disabled) {
  background: #f0f0f0;
  transform: scale(1.05);
}

.send-btn:disabled {
  cursor: not-allowed;
  background: #565869;
  color: #8e8ea0;
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