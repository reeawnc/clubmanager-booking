<template>
  <div class="home">
    <section class="grid">
      <button class="card" @click="go('courts')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><rect x="6" y="6" width="36" height="36" rx="4" fill="none" stroke="currentColor" stroke-width="2"/><line x1="6" y1="24" x2="42" y2="24" stroke="currentColor" stroke-width="2"/><line x1="24" y1="6" x2="24" y2="42" stroke="currentColor" stroke-width="2"/></svg>
        </div>
        <h3>Courts</h3>
        <p>Availability, who’s playing, time filters</p>
      </button>
      <button class="card" @click="go('booking')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><rect x="8" y="8" width="32" height="28" rx="3" fill="none" stroke="currentColor" stroke-width="2"/><path d="M12 16h24M16 22h16" stroke="currentColor" stroke-width="2"/></svg>
        </div>
        <h3>Book</h3>
        <p>Quick booking flows</p>
      </button>
      <button class="card" @click="go('mybookings')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><rect x="10" y="8" width="28" height="32" rx="3" fill="none" stroke="currentColor" stroke-width="2"/><path d="M16 16h16M16 22h16M16 28h12" stroke="currentColor" stroke-width="2"/></svg>
        </div>
        <h3>My bookings</h3>
        <p>Today and upcoming</p>
      </button>
      <button class="card" @click="go('messages')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><rect x="6" y="10" width="36" height="26" rx="4" fill="none" stroke="currentColor" stroke-width="2"/><path d="M6 12l18 12L42 12" fill="none" stroke="currentColor" stroke-width="2"/></svg>
        </div>
        <h3>Messages</h3>
        <p>Unread, inbox, sent</p>
      </button>
      <button class="card" @click="go('boxpositions')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><path d="M10 36h6V24h-6zM21 36h6V18h-6zM32 36h6V12h-6z" fill="currentColor"/></svg>
        </div>
        <h3>Box positions</h3>
        <p>Current standings</p>
      </button>
      <button class="card" @click="go('liveresults')">
        <div class="svg-wrap" aria-hidden="true">
          <svg viewBox="0 0 48 48" class="icon-svg"><path d="M8 30c6-8 12 8 18 0s12-16 14-18" fill="none" stroke="currentColor" stroke-width="2"/><circle cx="10" cy="32" r="2" fill="currentColor"/><circle cx="26" cy="30" r="2" fill="currentColor"/><circle cx="40" cy="12" r="2" fill="currentColor"/></svg>
        </div>
        <h3>Live results</h3>
        <p>Latest box matches</p>
      </button>
    </section>
  </div>
  
</template>

<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useChatStore } from '@/stores/chat'

const router = useRouter()
function go(category: string) {
  router.push({ path: '/chat', query: { c: category } })
}
</script>

<style scoped>
.home {
  min-height: 100vh;
  background: #0d1117;
  color: #e6edf3;
  display: flex;
  align-items: stretch;
}

.grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: 0.75rem;
  padding: 1rem;
  width: 100%;
  max-width: 720px;
  margin: 0 auto;
}
@media (min-width: 640px) {
  .grid { grid-template-columns: repeat(2, 1fr); }
}
@media (min-width: 1024px) {
  .grid { grid-template-columns: repeat(3, 1fr); }
}

.card {
  background: #0f1520;
  border: 1px solid #222b36;
  border-radius: 14px;
  padding: 1rem;
  text-align: left;
  color: inherit;
  cursor: pointer;
  transition: transform .15s ease, border-color .2s ease, box-shadow .2s ease;
}
.card:hover { transform: translateY(-2px); border-color: #58a6ff; box-shadow: 0 8px 24px rgba(88,166,255,.15); }
.svg-wrap { width: 44px; height: 44px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; background: #0b1220; border: 1px solid #1d2633; margin-bottom: .5rem; }
.icon-svg { width: 24px; height: 24px; color: #9fb4cc; }
.card h3 { margin: 0 0 0.2rem; font-size: 1rem; }
.card p { margin: 0; color: #7d8590; font-size: .9rem; }
</style>


