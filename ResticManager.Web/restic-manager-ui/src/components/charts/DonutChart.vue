<template>
  <div class="rm-donut" :style="{ '--size': size + 'px' }">
    <svg :viewBox="`0 0 ${size} ${size}`" class="rm-donut__svg">
      <circle :cx="size/2" :cy="size/2" :r="radius" fill="none" stroke="#243250" :stroke-width="stroke"/>
      <g :transform="`translate(${size/2}, ${size/2}) rotate(-90)`">
        <circle
          v-for="(seg, idx) in segments"
          :key="idx"
          :r="radius"
          fill="none"
          :stroke="seg.color"
          :stroke-width="stroke"
          :stroke-dasharray="`${seg.length} ${circumference}`"
          :stroke-dashoffset="-seg.offset"
          stroke-linecap="butt"
          class="rm-donut__seg"
        />
      </g>
      <text :x="size/2" :y="size/2 - 6" text-anchor="middle" font-size="22" font-weight="700" fill="#0f172a">{{ total }}</text>
      <text :x="size/2" :y="size/2 + 16" text-anchor="middle" font-size="11" fill="#64748b">{{ totalLabel }}</text>
    </svg>
    <ul class="rm-donut__legend">
      <li v-for="(seg, idx) in data" :key="idx">
        <span class="rm-donut__dot" :style="{ background: seg.color }"></span>
        <span class="rm-donut__label">{{ seg.label }}</span>
        <span class="rm-donut__value">{{ seg.value }}</span>
        <span class="rm-donut__pct">{{ seg.percent }}%</span>
      </li>
    </ul>
  </div>
</template>

<script>
export default {
  name: 'DonutChart',
  props: {
    data: { type: Array, required: true },
    size: { type: Number, default: 180 },
    stroke: { type: Number, default: 22 },
    totalLabel: { type: String, default: '总数' }
  },
  computed: {
    radius() {
      return (this.size - this.stroke) / 2;
    },
    circumference() {
      return 2 * Math.PI * this.radius;
    },
    total() {
      return this.data.reduce((s, d) => s + d.value, 0);
    },
    segments() {
      const palette = ['#06b6d4', '#22c55e', '#f59e0b', '#ec4899', '#0ea5e9', '#a855f7'];
      let acc = 0;
      return this.data.map((d, idx) => {
        const ratio = this.total ? d.value / this.total : 0;
        d.percent = Math.round(ratio * 100);
        d.color = d.color || palette[idx % palette.length];
        const length = ratio * this.circumference;
        const seg = {
          length,
          offset: acc,
          color: d.color
        };
        acc += length;
        return seg;
      });
    }
  }
};
</script>

<style scoped>
.rm-donut {
  display: flex;
  align-items: center;
  gap: 28px;
}
.rm-donut__svg {
  width: var(--size);
  height: var(--size);
  flex-shrink: 0;
}
.rm-donut__seg {
  transition: opacity 200ms;
}
.rm-donut__seg:hover { opacity: 0.85; }
.rm-donut__legend {
  list-style: none;
  padding: 0;
  margin: 0;
  flex: 1;
  min-width: 0;
}
.rm-donut__legend li {
  display: grid;
  grid-template-columns: 12px 1fr auto auto;
  align-items: center;
  gap: 10px;
  padding: 8px 0;
  font-size: 13px;
  border-bottom: 1px dashed var(--color-border-soft);
}
.rm-donut__legend li:last-child { border-bottom: 0; }
.rm-donut__dot {
  width: 10px;
  height: 10px;
  border-radius: 3px;
}
.rm-donut__label { color: var(--color-text-regular); }
.rm-donut__value { font-weight: 600; color: var(--color-text-primary); }
.rm-donut__pct { font-size: 11px; color: var(--color-text-secondary); width: 36px; text-align: right; }
</style>