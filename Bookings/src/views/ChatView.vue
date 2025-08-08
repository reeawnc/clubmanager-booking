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

    <!-- Home button - stacked under clear when visible -->
    <button
      class="home-button"
      :class="messages.length > 0 ? 'home-below' : 'home-top'"
      title="Home"
      aria-label="Home"
      @click="goHome"
    >
      <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M3 11l9-7 9 7"></path>
        <path d="M5 10v10a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1v-5h2v5a1 1 0 0 0 1 1h4a1 1 0 0 0 1-1V10"></path>
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
        <!-- Home circle already present globally; no duplicate needed here -->
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
            :placeholder="placeholderText"
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
import { ref, nextTick, onMounted, onUnmounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useChatStore } from '@/stores/chat'
import ChatMessage from '@/components/ChatMessage.vue'

const chatStore = useChatStore()
const route = useRoute()
const router = useRouter()
const { messages, isLoading } = storeToRefs(chatStore)
const { sendMessage: storeSendMessage, clearChat } = chatStore

const inputMessage = ref('')
const messageInput = ref<HTMLTextAreaElement>()
const messagesContainer = ref<HTMLElement>()

const CATEGORY_PROMPTS: Record<string, { id: number; text: string }[]> = {
  courts: [
    { id: 1, text: 'Show me the court timetable for today after 5pm' },
    { id: 2, text: 'What time slots are available tomorrow after 5pm?' },
    { id: 3, text: "Who\'s playing on Court 2 at 19:00 today?" },
    { id: 4, text: 'Show me only bookable slots after 18:30 today' },
    { id: 5, text: 'Who is playing tonight?' },
    { id: 6, text: 'Show availability for Court 1 and Court 3 between 18:00 and 20:00' },
    { id: 7, text: 'Show me the court timetable between 6pm and 7:30 for Monday, Tuesday, Wednesday and Thursday this week' },
  ],
  booking: [
    { id: 10, text: 'Book a court for 18:00 today' },
    { id: 11, text: 'Book a court for 18:45 this day next week' },
    { id: 12, text: 'Book a 45-minute slot between 19:00 and 20:00 tomorrow' },
    { id: 13, text: 'Find the next available 45-minute slot tonight and book it' },
  ],
  mybookings: [
    { id: 20, text: 'Show my bookings' },
    { id: 21, text: 'Cancel my next booking' },
    { id: 22, text: 'What time is my next booking?' },
  ],
  messages: [
    { id: 30, text: 'Do I have any unread messages?' },
    { id: 31, text: 'Show my inbox messages' },
    { id: 32, text: 'Show my sent messages' },
    { id: 33, text: 'Search my messages for “league”' },
  ],
  boxpositions: [
    { id: 40, text: 'Show current box positions for Club' },
    { id: 41, text: 'Show current box positions for SummerFriendlies' },
    { id: 42, text: 'Summarize top 3 in each box for SummerFriendlies' },
  ],
  liveresults: [
    { id: 50, text: 'Live results: Show current SummerFriendlies match results' },
    { id: 51, text: 'Live results: Show current Club match results' },
    { id: 52, text: 'Live results: Show current results for Box A1 only' },
  ],
}

const selectedCategory = ref<string>('courts')
const quickPrompts = ref<{ id: number; text: string }[]>(CATEGORY_PROMPTS[selectedCategory.value])

const placeholderText = computed(() => {
  const map: Record<string, string> = {
    courts: 'Ask about availability, times or who\'s playing…',
    booking: 'Describe the slot you\'d like to book…',
    mybookings: 'Ask about your upcoming bookings…',
    messages: 'Ask about unread, inbox or sent messages…',
    boxpositions: 'Ask for current standings or summaries…',
    liveresults: 'Ask for current box results (e.g., Box A1)…',
  }
  return map[selectedCategory.value] || 'Ask your question…'
})

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
const goHome = () => {
  clearChat()
  router.push('/')
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
  // Pick category from query and update contextual prompts
  const c = (route.query.c as string) || ''
  if (c && CATEGORY_PROMPTS[c]) {
    selectedCategory.value = c
    quickPrompts.value = CATEGORY_PROMPTS[c]
  }
  // Clean the query so refresh doesn't replay
  if (Object.keys(route.query).length > 0) {
    router.replace({ path: route.path, query: {} })
  }
  
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

.home-button {
  position: fixed;
  top: 1rem; /* default top when clear not shown */
  right: 1rem;
  z-index: 99;
  background: #21262d;
  border: 1px solid #30363d;
  color: #7d8590;
  padding: 0.6rem;
  border-radius: 50%;
  width: 40px;
  height: 40px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: all 0.2s ease;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
  backdrop-filter: blur(8px);
}
.home-button:hover { background: #30363d; color: #e6edf3; border-color: #58a6ff; transform: translateY(-1px); box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4); }

/* When messages exist, animate the home button below the clear button */
.home-below { top: 4.2rem; transition: top .2s ease; }
.home-top { top: 1rem; transition: top .2s ease; }

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

/* removed old inline Home link styles to keep circular consistency */

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