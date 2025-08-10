<template>
  <div class="message" :class="{ 'user-message': message.isUser, 'assistant-message': !message.isUser }">
    <div class="message-content">
      <!-- Avatars removed for a cleaner UI -->
      
      <div class="message-body">
        <div class="message-text">
          <div v-if="message.isLoading" class="typing-indicator">
            <div class="typing-dots">
              <span></span>
              <span></span>
              <span></span>
            </div>
          </div>
          <div v-else>
            <!-- Date header if available -->
            <div v-if="message.metadata?.fullDate && !message.isUser" class="date-header">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                <line x1="16" y1="2" x2="16" y2="6"></line>
                <line x1="8" y1="2" x2="8" y2="6"></line>
                <line x1="3" y1="10" x2="21" y2="10"></line>
              </svg>
              <span>{{ formatDate(message.metadata.fullDate) }}</span>
            </div>
            
            <!-- Simple enhanced message formatting -->
            <div class="message-content-text" v-html="formatMessage(message.content)"></div>
          </div>
        </div>
        <div class="message-time">
          {{ formatTime(message.timestamp) }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { Message } from '@/stores/chat'

defineProps<{
  message: Message
}>()

const formatTime = (date: Date) => {
  return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
}

const formatDate = (dateString: string) => {
  try {
    const date = new Date(dateString)
    const today = new Date()
    
    if (date.toDateString() === today.toDateString()) {
      return 'Today'
    }
    
    const tomorrow = new Date(today)
    tomorrow.setDate(today.getDate() + 1)
    if (date.toDateString() === tomorrow.toDateString()) {
      return 'Tomorrow'
    }
    
    return date.toLocaleDateString([], { 
      weekday: 'long', 
      year: 'numeric', 
      month: 'long', 
      day: 'numeric' 
    })
  } catch {
    return dateString
  }
}

const formatMessage = (content: string) => {
  // Simple, robust formatting. Preserve any returned HTML (tables) from the LLM.
  let formatted = content
  
  // Headers with subtle styling
  formatted = formatted.replace(/^### (.*$)/gm, '<h3 class="section-header">$1</h3>')
  formatted = formatted.replace(/^## (.*$)/gm, '<h2>$2</h2>')
  formatted = formatted.replace(/^# (.*$)/gm, '<h1>$1</h1>')
  
  // Bold text
  formatted = formatted.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
  
  // Time slots with subtle enhancement
  formatted = formatted.replace(/(\d{2}:\d{2} - \d{2}:\d{2}): (.*?)$/gm, (match, time, description) => {
    const icon = description.toLowerCase().includes('training') ? '🏋️' : 
                 description.toLowerCase().includes('bookable') ? '✅' : 
                 description.toLowerCase().includes('unavailable') ? '❌' : '👤'
    return `<span class="time-slot">${time}: ${icon} ${description}</span>`
  })
  
  // Code blocks first (preserve whitespace monospacing)
  formatted = formatted.replace(/```(.*?)```/gs, '<pre><code>$1</code></pre>')
  
  // Line breaks: avoid injecting <br> inside <pre> or table HTML
  if (!/(<table|<tr|<td|<th|<pre)/i.test(formatted)) {
    formatted = formatted.replace(/\n/g, '<br>')
  }
  formatted = formatted.replace(/`(.*?)`/g, '<code>$1</code>')
  
  return formatted
}
</script>

<style scoped>
.message {
  margin-bottom: 1.5rem;
}

.message-content {
  display: flex;
  gap: 0.75rem;
  align-items: flex-start;
}

/* Avatars removed */

.message-body {
  flex: 1;
  min-width: 0;
}

.message-text {
  background: #21262d; /* Darker like cursor theme */
  border-radius: 0.75rem;
  padding: 0.875rem 1.125rem;
  margin-bottom: 0.25rem;
}

.user-message .message-text {
  background: #10a37f;
  color: white;
  margin-left: auto;
  max-width: 80%;
}

.assistant-message .message-text {
  background: #21262d; /* Darker like cursor theme */
  color: #e6edf3; /* Cursor theme text color */
  max-width: 100%;
}

.message-content-text {
  line-height: 1.6;
  word-wrap: break-word;
}

/* Ensure LLM-sent tables render nicely and do not get overridden */
.message-content-text :deep(table) {
  width: 100%;
  border-collapse: collapse;
}
.message-content-text :deep(th),
.message-content-text :deep(td) {
  padding: 8px 10px;
}
.message-content-text :deep(th) {
  text-align: left;
}
.message-content-text :deep(td[align='right']),
.message-content-text :deep(th[align='right']) {
  text-align: right;
}
.message-content-text :deep(.table-scroll) {
  overflow-x: auto;
}

.message-content-text :deep(h1),
.message-content-text :deep(h2),
.message-content-text :deep(h3) {
  margin: 0.75rem 0 0.5rem 0;
  color: #fff;
  font-weight: 600;
}

.message-content-text :deep(h1) { font-size: 1.25rem; }
.message-content-text :deep(h2) { font-size: 1.125rem; }
.message-content-text :deep(h3) { font-size: 1rem; }

.message-content-text :deep(ul) {
  margin: 0.5rem 0;
  padding-left: 1.5rem;
}

.message-content-text :deep(li) {
  margin: 0.25rem 0;
}

.message-content-text :deep(strong) {
  color: #fff;
  font-weight: 600;
}

.message-content-text :deep(code) {
  background: rgba(255, 255, 255, 0.1);
  padding: 0.125rem 0.25rem;
  border-radius: 0.25rem;
  font-family: 'Courier New', monospace;
  font-size: 0.875rem;
}

.message-content-text :deep(pre) {
  background: rgba(255, 255, 255, 0.05);
  padding: 0.75rem;
  border-radius: 0.375rem;
  margin: 0.5rem 0;
  overflow-x: auto;
}

.message-content-text :deep(pre code) {
  background: none;
  padding: 0;
}

/* Simple enhanced styling */
.date-header {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 1rem;
  padding: 0.5rem 0.75rem;
  background: rgba(16, 163, 127, 0.1);
  border-left: 3px solid #10a37f;
  border-radius: 0.375rem;
  font-size: 0.875rem;
  color: #10a37f;
  font-weight: 500;
}

.message-content-text :deep(.section-header) {
  color: #10a37f;
  font-size: 1.1rem;
  font-weight: 600;
  margin: 1rem 0 0.5rem 0;
  padding-bottom: 0.25rem;
  border-bottom: 1px solid rgba(16, 163, 127, 0.3);
}

.message-content-text :deep(.time-slot) {
  display: inline-block;
  margin: 0.125rem 0;
  font-family: inherit;
}

.message-time {
  font-size: 0.75rem;
  color: #666;
  margin-left: 0.25rem;
}

.typing-indicator {
  display: flex;
  align-items: center;
  padding: 0.25rem 0;
}

.typing-dots {
  display: flex;
  gap: 0.25rem;
}

.typing-dots span {
  width: 6px;
  height: 6px;
  background: #666;
  border-radius: 50%;
  animation: bounce 1.4s infinite ease-in-out;
}

.typing-dots span:nth-child(1) { animation-delay: -0.32s; }
.typing-dots span:nth-child(2) { animation-delay: -0.16s; }

@keyframes bounce {
  0%, 80%, 100% {
    transform: scale(0.8);
    opacity: 0.5;
  }
  40% {
    transform: scale(1);
    opacity: 1;
  }
}

.user-message .message-content {
  flex-direction: row-reverse;
}

.user-message .message-time {
  text-align: right;
  margin-left: 0;
  margin-right: 0.25rem;
}
</style>