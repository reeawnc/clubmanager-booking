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
      id: Date.now().toString(),
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
    console.log('updateMessage called:', { id, content: content.substring(0, 50) + '...', isLoading: false })
    const messageIndex = messages.value.findIndex(m => m.id === id)
    if (messageIndex !== -1) {
      // Force Vue reactivity by creating a new message object
      const updatedMessage = {
        ...messages.value[messageIndex],
        content,
        isLoading: false,
        metadata: metadata ? { ...messages.value[messageIndex].metadata, ...metadata } : messages.value[messageIndex].metadata
      }
      messages.value[messageIndex] = updatedMessage
      console.log('Message updated, isLoading set to false for message:', id)
    } else {
      console.error('Message not found for id:', id)
    }
  }

  const sendMessage = async (prompt: string) => {
    if (!prompt.trim()) return

    // Add user message
    addMessage(prompt, true)

    // Add loading assistant message
    const assistantMessage = addMessage('', false)
    console.log('Created assistant message with id:', assistantMessage.id, 'isLoading:', assistantMessage.isLoading)
    isLoading.value = true

    try {
      // For local development, call the Azure Functions directly
      // For production (Azure Static Web Apps), this would be '/api/PromptFunction'
      const apiUrl = import.meta.env.DEV 
        ? 'http://localhost:7071/api/PromptFunction'  // Local development
        : '/api/PromptFunction';  // Production (Azure Static Web Apps)
      
      const response = await fetch(apiUrl, {
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

      if (!response.ok) {
        const errorText = await response.text()
        console.error('API Error:', errorText)
        throw new Error(`HTTP error! status: ${response.status}, message: ${errorText}`)
      }

      const data: ApiResponse = await response.json()
      
      if (data.success) {
        sessionId.value = data.sessionId
        
        // Extract metadata from toolCalls
        const metadata: any = {
          toolCalls: data.toolCalls
        }
        
        // Look for date information in toolCalls
        if (data.toolCalls && Array.isArray(data.toolCalls)) {
          data.toolCalls.forEach(toolCall => {
            if (toolCall.parameters && toolCall.parameters.FullDate) {
              metadata.fullDate = toolCall.parameters.FullDate
            }
          })
        }

        // Determine query type based on prompt content
        if (prompt.toLowerCase().includes('availability')) {
          metadata.queryType = 'availability'
        } else if (prompt.toLowerCase().includes('playing') || prompt.toLowerCase().includes('who')) {
          metadata.queryType = 'players'
        }
        
        console.log('Updating message with response, assistantMessage.id:', assistantMessage.id)
        updateMessage(assistantMessage.id, data.response, metadata)
      } else {
        updateMessage(assistantMessage.id, `Error: ${data.errorMessage || 'Unknown error occurred'}`)
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