import { defineStore } from 'pinia'
import { ref } from 'vue'

export interface Message {
  id: string
  content: string
  isUser: boolean
  timestamp: Date
  isLoading?: boolean
  metadata?: {
    fullDate?: string
    toolCalls?: any[]
    queryType?: string
  }
}

export interface ApiResponse {
  response: string
  sessionId: string
  success: boolean
  errorMessage: string | null
  toolCalls?: any[]
}

export const useChatStore = defineStore('chat', () => {
  const messages = ref<Message[]>([])
  const sessionId = ref<string>('')
  const isLoading = ref(false)

  const addMessage = (content: string, isUser: boolean = false, metadata?: any) => {
    const message: Message = {
      id: Date.now().toString() + Math.random().toString(36).substr(2, 9), // Ensure unique ID
      content,
      isUser,
      timestamp: new Date(),
      isLoading: !isUser && content === '',
      metadata
    }
    messages.value.push(message)
    return message
  }

  const updateMessage = (id: string, content: string, metadata?: any) => {
    const messageIndex = messages.value.findIndex(m => m.id === id)
    if (messageIndex !== -1) {
      messages.value[messageIndex] = {
        ...messages.value[messageIndex],
        content,
        isLoading: false,
        metadata: metadata ? { ...messages.value[messageIndex].metadata, ...metadata } : messages.value[messageIndex].metadata
      }
    }
  }

  const sendMessage = async (prompt: string) => {
    if (!prompt.trim()) return

    // Add user message
    addMessage(prompt, true)

    // Add loading assistant message
    const assistantMessage = addMessage('', false)
    isLoading.value = true

    try {
      // Toggle streaming via env. Default: non-stream in dev to avoid CORS pain.
      const useStream = (import.meta as any).env?.VITE_USE_STREAM === 'true'
      const apiUrl = useStream
        ? (import.meta.env.DEV ? 'http://localhost:7071/api/PromptFunction/stream' : '/api/PromptFunction/stream')
        : (import.meta.env.DEV ? 'http://localhost:7071/api/PromptFunction' : '/api/PromptFunction')

      let response: Response
      try {
        response = await fetch(apiUrl, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            prompt,
            userId: 'testuser123',
            sessionId: sessionId.value || 'testsession456'
          }),
        })
      } catch (e) {
        // Hard fallback on network error
        const nonStreamUrl = import.meta.env.DEV ? 'http://localhost:7071/api/PromptFunction' : '/api/PromptFunction'
        const nonStream = await fetch(nonStreamUrl, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({
            prompt,
            userId: 'testuser123',
            sessionId: sessionId.value || 'testsession456'
          }),
        })
        if (!nonStream.ok) {
          const errText = await nonStream.text()
          throw new Error(`HTTP error! status: ${nonStream.status}, message: ${errText}`)
        }
        const data: any = await nonStream.json()
        updateMessage(assistantMessage.id, data.response)
        return
      }

      if (!response.ok) {
        const errorText = await response.text()
        console.error('API Error:', errorText)
        throw new Error(`HTTP error! status: ${response.status}, message: ${errorText}`)
      }

      // If using stream, read progressively; otherwise parse JSON
      if (useStream) {
        if (!response.body) {
          const text = await response.text()
          updateMessage(assistantMessage.id, text)
          return
        }
        const reader = response.body.getReader()
        const decoder = new TextDecoder()
        let partial = ''
        while (true) {
          const { value, done } = await reader.read()
          if (done) break
          partial += decoder.decode(value, { stream: true })
          updateMessage(assistantMessage.id, partial)
        }
        partial += decoder.decode()
        updateMessage(assistantMessage.id, partial)
      } else {
        const data: any = await response.json()
        updateMessage(assistantMessage.id, data.response)
      }
    } catch (error) {
      console.error('Error sending message:', error)
      if (error instanceof TypeError && error.message.includes('Failed to fetch')) {
        updateMessage(assistantMessage.id, 'Connection failed. Please ensure your Azure Function is running on localhost:7071 and try again.')
      } else {
        updateMessage(assistantMessage.id, `Error: ${error instanceof Error ? error.message : 'Unknown error occurred'}`)
      }
    } finally {
      isLoading.value = false
    }
  }

  const clearChat = () => {
    messages.value = []
    sessionId.value = ''
  }

  return {
    messages,
    sessionId,
    isLoading,
    addMessage,
    sendMessage,
    clearChat
  }
})