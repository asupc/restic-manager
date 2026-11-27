<template>
  <div class="rm-chart" ref="root" @mouseleave="hover = -1">
    <div class="rm-chart__canvas">
      <svg
        :viewBox="`0 0 ${width} ${height}`"
        :style="{ height: height + 'px' }"
        preserveAspectRatio="none"
        class="rm-chart__svg"
      >
        <defs>
          <linearGradient :id="gradId" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" :stop-color="resolvedColor" stop-opacity="0.30" />
            <stop offset="100%" :stop-color="resolvedColor" stop-opacity="0" />
          </linearGradient>
        </defs>

        <!-- 水平网格线 -->
        <g v-if="showGrid">
          <line
            v-for="i in 4"
            :key="'g' + i"
            :x1="padX"
            :x2="width - padX"
            :y1="padY + ((height - padY * 2) / 4) * i"
            :y2="padY + ((height - padY * 2) / 4) * i"
            class="rm-chart__grid"
            stroke-dasharray="3 4"
          />
        </g>

        <!-- 垂直悬浮参考线 -->
        <line
          v-if="activePoint"
          :x1="activePoint.x"
          :x2="activePoint.x"
          :y1="padY"
          :y2="height - padY"
          class="rm-chart__guide"
          stroke-dasharray="4 3"
        />

        <!-- 面积填充 -->
        <path v-if="areaPath" :d="areaPath" :fill="`url(#${gradId})`" />

        <!-- 折线 -->
        <path
          v-if="linePath"
          :d="linePath"
          :stroke="resolvedColor"
          stroke-width="2.4"
          fill="none"
          stroke-linecap="round"
          stroke-linejoin="round"
        />

        <!-- 悬浮高光外环 -->
        <circle
          v-if="activePoint"
          :cx="activePoint.x"
          :cy="activePoint.y"
          r="9"
          :fill="resolvedColor"
          fill-opacity="0.18"
          class="rm-chart__halo"
        />

        <!-- 数据点 -->
        <circle
          v-for="(pt, idx) in points"
          :key="'pt-' + idx"
          :cx="pt.x"
          :cy="pt.y"
          :r="hover === idx ? 5.5 : 3.5"
          :fill="resolvedColor"
          stroke-width="2"
          class="rm-chart__dot"
          :class="{ 'is-active': hover === idx }"
        />

        <!-- 按列感应热区（提升悬浮体验） -->
        <rect
          v-for="(zone, idx) in hitZones"
          :key="'hz-' + idx"
          :x="zone.x"
          :y="0"
          :width="zone.width"
          :height="height"
          fill="transparent"
          class="rm-chart__hit"
          @mouseenter="hover = idx"
        />
      </svg>

      <!-- Y 轴刻度参考 -->
      <div class="rm-chart__y-axis" v-if="showGrid">
        <span>{{ max }}</span>
        <span>{{ Math.round(max / 2) }}</span>
        <span>0</span>
      </div>

      <!-- 浮动卡片 Tooltip -->
      <div
        v-if="activePoint"
        class="rm-chart__tooltip"
        :style="tooltipStyle"
      >
        <div class="rm-chart__tooltip-title">{{ activePoint.label || ('#' + (hover + 1)) }}</div>
        <div class="rm-chart__tooltip-row">
          <span class="rm-chart__tooltip-dot" :style="{ background: resolvedColor }"></span>
          <span class="rm-chart__tooltip-name">{{ seriesLabel }}</span>
          <span class="rm-chart__tooltip-val">{{ activePoint.value }}</span>
        </div>
      </div>
    </div>

    <!-- X 轴标签 -->
    <div class="rm-chart__labels" v-if="labels && labels.length">
      <span
        v-for="(l, idx) in labels"
        :key="idx"
        :class="{
          'rm-chart__labels--hidden': idx % labelStep !== 0 && hover !== idx,
          'is-active': hover === idx
        }"
      >{{ l }}</span>
    </div>
  </div>
</template>

<script>
let lineChartUid = 0;

export default {
  name: 'LineChart',
  props: {
    data: { type: Array, required: true },
    labels: { type: Array, default: () => [] },
    color: { type: String, default: '' },
    seriesLabel: { type: String, default: '数值' },
    height: { type: Number, default: 240 },
    width: { type: Number, default: 600 },
    showGrid: { type: Boolean, default: true }
  },
  data() {
    return {
      gradId: 'lg-' + (++lineChartUid),
      padX: 18,
      padY: 18,
      hover: -1
    };
  },
  computed: {
    resolvedColor() {
      return this.color || 'var(--brand-500)';
    },
    max() {
      const nums = (this.data || []).map(v => Number(v) || 0);
      const m = Math.max(...nums, 1);
      return Math.ceil(m / 10) * 10 || 10;
    },
    stepX() {
      return (this.width - this.padX * 2) / Math.max((this.data || []).length - 1, 1);
    },
    points() {
      const list = this.data || [];
      const len = list.length;
      return list.map((raw, i) => {
        const v = Number(raw) || 0;
        return {
          x: len === 1 ? this.width / 2 : this.padX + this.stepX * i,
          y: this.padY + (1 - v / this.max) * (this.height - this.padY * 2),
          value: v,
          label: (this.labels && this.labels[i]) ? this.labels[i] : ''
        };
      });
    },
    hitZones() {
      const pts = this.points;
      const len = pts.length;
      if (len === 0) return [];
      if (len === 1) {
        return [{ x: 0, width: this.width }];
      }
      return pts.map((pt, i) => {
        const left = i === 0 ? 0 : (pts[i - 1].x + pt.x) / 2;
        const right = i === len - 1 ? this.width : (pt.x + pts[i + 1].x) / 2;
        return { x: left, width: Math.max(right - left, 1) };
      });
    },
    activePoint() {
      if (this.hover < 0 || this.hover >= this.points.length) return null;
      return this.points[this.hover];
    },
    tooltipStyle() {
      if (!this.activePoint) return {};
      const xPct = (this.activePoint.x / this.width) * 100;
      const yPct = (this.activePoint.y / this.height) * 100;
      const placeLeft = xPct > 72;
      const placeBelow = yPct < 24;
      const clampedX = Math.max(4, Math.min(96, xPct));
      return {
        left: clampedX + '%',
        top: Math.max(8, Math.min(86, yPct)) + '%',
        transform: `translate(${placeLeft ? 'calc(-100% - 12px)' : '12px'}, ${placeBelow ? '8px' : '-100%'})`
      };
    },
    linePath() {
      if (this.points.length < 2) return '';
      return this.points
        .map((p, i) => (i === 0 ? `M${p.x},${p.y}` : `L${p.x},${p.y}`))
        .join(' ');
    },
    areaPath() {
      if (this.points.length < 2) return '';
      const first = this.points[0];
      const last = this.points[this.points.length - 1];
      const bottom = this.height - this.padY;
      const line = this.points
        .map((p, i) => (i === 0 ? `M${p.x},${p.y}` : `L${p.x},${p.y}`))
        .join(' ');
      return `${line} L${last.x},${bottom} L${first.x},${bottom} Z`;
    },
    labelStep() {
      return Math.max(1, Math.ceil((this.labels || []).length / 8));
    }
  }
};
</script>

<style scoped>
.rm-chart {
  width: 100%;
}

.rm-chart__canvas {
  position: relative;
  width: 100%;
}

.rm-chart__svg {
  width: 100%;
  display: block;
}

.rm-chart__grid {
  stroke: var(--color-border);
  stroke-width: 1;
}

.rm-chart__guide {
  stroke: var(--brand-400);
  stroke-opacity: 0.55;
  stroke-width: 1.2;
  pointer-events: none;
}

.rm-chart__halo {
  pointer-events: none;
}

.rm-chart__dot {
  stroke: var(--color-bg-card);
  transition: r 150ms ease;
  pointer-events: none;
}

.rm-chart__hit {
  cursor: crosshair;
}

.rm-chart__y-axis {
  position: absolute;
  top: 8px;
  bottom: 12px;
  left: 4px;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  pointer-events: none;
  font-size: 10px;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  opacity: 0.75;
}

.rm-chart__tooltip {
  position: absolute;
  z-index: 10;
  pointer-events: none;
  min-width: 112px;
  padding: 8px 10px;
  border-radius: var(--radius-sm, 6px);
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  box-shadow: var(--shadow-lg);
  transition: left 100ms ease, top 100ms ease;
}

.rm-chart__tooltip-title {
  font-size: 11px;
  color: var(--color-text-secondary);
  margin-bottom: 4px;
  white-space: nowrap;
}

.rm-chart__tooltip-row {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}

.rm-chart__tooltip-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}

.rm-chart__tooltip-name {
  color: var(--color-text-secondary);
}

.rm-chart__tooltip-val {
  margin-left: auto;
  font-weight: 700;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.rm-chart__labels {
  display: flex;
  justify-content: space-between;
  padding: 8px 12px 0;
  font-size: 11px;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
}

.rm-chart__labels span {
  transition: color 150ms ease;
}

.rm-chart__labels span.is-active {
  color: var(--color-text-primary);
  font-weight: 600;
}

.rm-chart__labels--hidden {
  visibility: hidden;
}
</style>