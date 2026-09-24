<template>
  <div class="shell">
    <header class="topbar">
      <NuxtLink class="brand" to="/">AgentDispatcher</NuxtLink>
      <nav class="nav">
        <NuxtLink to="/">概要</NuxtLink>
        <NuxtLink to="/projects">プロジェクト</NuxtLink>
        <NuxtLink to="/executions">実行履歴</NuxtLink>
        <NuxtLink to="/health">実行環境</NuxtLink>
      </nav>
    </header>
    <main class="content">
      <NuxtPage />
    </main>
  </div>
</template>

<style src="~/assets/main.css" />
