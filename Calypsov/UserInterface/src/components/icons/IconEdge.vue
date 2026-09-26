<!--
  Unlike the other icons here, this is a full-color brand mark (Edge's logo is only
  recognizable in its actual colors) rather than a `currentColor` line icon. This is a
  stylized approximation of the swoosh, not a pixel-accurate trace of the official mark.
-->
<template>
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <defs>
      <linearGradient id="edge-base" x1="2" y1="4" x2="22" y2="20" gradientUnits="userSpaceOnUse">
        <stop offset="0" stop-color="#0F6CBD" />
        <stop offset="0.55" stop-color="#1B9DE8" />
        <stop offset="1" stop-color="#37C4A6" />
      </linearGradient>
      <linearGradient id="edge-swoosh" x1="4" y1="18" x2="20" y2="6" gradientUnits="userSpaceOnUse">
        <stop offset="0" stop-color="#0B4A8C" />
        <stop offset="1" stop-color="#1B9DE8" />
      </linearGradient>
    </defs>
    <circle cx="12" cy="12" r="10" fill="url(#edge-base)" />
    <path
      d="M4.5 15.2c1.6 3.4 5.1 5.3 9 4.6 3.1-.6 5.3-2.6 6.2-5-1.9 1.7-4.4 2.4-6.9 1.7-3-.8-5-3.2-5.6-6-1 1.5-1.6 3.1-1.6 4.2 0 .2 0 .3 0 .5Z"
      fill="url(#edge-swoosh)"
    />
    <path
      d="M8.4 5.1C6 6.4 4.4 8.7 4 11.4c.5-1.6 1.9-3 3.8-3.5 2.5-.7 5 .3 6.4 2.3 1.1 1.6 1.3 3.4.8 5 1.7-.9 2.9-2.6 3-4.6.2-3.9-3.1-7.2-7.4-7.1-.8 0-1.6.2-2.2.6Z"
      fill="#ffffff"
      fill-opacity="0.18"
    />
  </svg>
</template>
