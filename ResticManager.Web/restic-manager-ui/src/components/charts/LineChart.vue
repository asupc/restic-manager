<template>
  <div class="rm-chart" ref="root">
    <svg :viewBox="`0 0 ${width} ${height}`" preserveAspectRatio="none" class="rm-chart__svg">
      <defs>
        <linearGradient :id="gradId" x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" :stop-color="color" stop-opacity="0.32"/>
          <stop offset="100%" :stop-color="color" stop-opacity="0"/>
        </linearGradient>
      </defs>

      <!-- 网格 -->
      <g v-if="showGrid">
        <line
          v-for="i in 4" :key="'g' + i"
          :x1="padX" :x2="width - padX"
          :y1="padY + ((height - padY * 2) / 4) * i"
          :y2="padY + ((height - padY * 2) / 4) * i"
          stroke="#243250" stroke-dasharray="3 4"
        />
      </g>

      <!-- 面积 -->
      <path :d="areaPath" :fill="`url(#${gradId})`" />

      <!-- 折线 -->
      <path :d="linePath" :stroke="color" stroke-width="2.4" fill="none" stroke-linecap="round" stroke-linejoin="round" />

      <!-- 数据点 + 提示 -->
      <g v-for="(pt, idx) in points" :key="idx">
        <circle
          :cx="pt.x" :cy="pt.y" r="4"
          :fill="color" stroke="#101a2f" stroke-width="2"
          @mouseenter="hover = idx" @mouseleave="hover = -1"
          class="rm-chart__dot"
        />
        <g v-if="hover === idx" :transform="`translate(${pt.x}, ${pt.y - 14})`" class="rm-chart__tip">
          <rect x="-30" y="-22" width="60" height="22" rx="6" fill="#334155" />
          <text x="0" y="-7" text-anchor="middle" fill="#fff" font-size="11" font-weight="600">{{ pt.value }}</text>
        </g>
      </g>
    </svg>
    <!-- X轴标签 -->
    <div class="rm-chart__labels" v-if="labels && labels.length">
      <span v-for="(l, idx) in labels" :key="idx" :class="{ 'rm-chart__labels--hidden': idx % labelStep !== 0 }">{{ l }}</span>
    </div>
  </div>
</template>

<script>
export default {
  name: 'LineChart',
  props: {
    data: { type: Array, required: true },
    labels: { type: Array, default: () => [] },
    color: { type: String, default: '#06b6d4' },
    height: { type: Number, default: 240 },
    width: { type: Number, default: 600 },
    showGrid: { type: Boolean, default: true }
  },
  data() {
    return {
      padX: 16,
      padY: 16,
      hover: -1
    };
  },
  computed: {
    gradId() {
      return 'lg-' + Math.random().toString(36).slice(2, 8);
    },
    max() {
      const m = Math.max(...this.data, 1);
      return Math.ceil(m / 10) * 10 || 10;
    },
    stepX() {
      return (this.width - this.padX * 2) / Math.max(this.data.length - 1, 1);
    },
    points() {
      return this.data.map((v, i) => ({
        x: this.padX + this.stepX * i,
        y: this.padY + (1 - v / this.max) * (this.height - this.padY * 2),
        value: v
      }));
    },
    linePath() {
      if (!this.points.length) return '';
      return this.points
        .map((p, i) => (i === 0 ? `M${p.x},${p.y}` : `L${p.x},${p.y}`))
        .join(' ');
    },
    areaPath() {
      if (!this.points.length) return '';
      const first = this.points[0];
      const last = this.points[this.points.length - 1];
      const bottom = this.height - this.padY;
      const line = this.points
        .map((p, i) => (i === 0 ? `M${p.x},${p.y}` : `L${p.x},${p.y}`))
        .join(' ');
      return `${line} L${last.x},${bottom} L${first.x},${bottom} Z`;
    },
    labelStep() {
      return Math.max(1, Math.ceil(this.labels.length / 8));
    }
  }
};
</script>

<style scoped>
.rm-chart {
  width: 100%;
}
.rm-chart__svg {
  width: 100%;
  height: 240px;
  display: block;
}
.rm-chart__dot {
  cursor: pointer;
  transition: r 150ms;
}
.rm-chart__dot:hover { r: 6; }
.rm-chart__tip { pointer-events: none; }

.rm-chart__labels {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px 0;
  font-size: 11px;
  color: var(--color-text-secondary);
}
.rm-chart__labels--hidden { visibility: hidden; }
</style>