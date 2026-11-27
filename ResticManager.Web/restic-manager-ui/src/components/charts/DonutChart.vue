<template>
  <div class="rm-donut" :style="{ '--size': size + 'px' }">
    <div class="rm-donut__chart">
      <svg :viewBox="`0 0 ${size} ${size}`" class="rm-donut__svg">
        <!-- 底环轨道 -->
        <circle
          :cx="size / 2"
          :cy="size / 2"
          :r="radius"
          fill="none"
          :stroke-width="stroke"
          class="rm-donut__track"
        />
        <!-- 数据分段 -->
        <g :transform="`translate(${size / 2}, ${size / 2}) rotate(-90)`">
          <circle
            v-for="(seg, idx) in segments"
            :key="idx"
            :r="radius"
            fill="none"
            :stroke="seg.color"
            :stroke-width="hoveredIndex === idx ? stroke + 4 : stroke"
            :stroke-dasharray="`${seg.length} ${circumference}`"
            :stroke-dashoffset="-seg.offset"
            stroke-linecap="butt"
            class="rm-donut__seg"
            :class="{
              'is-active': hoveredIndex === idx,
              'is-dimmed': hoveredIndex !== -1 && hoveredIndex !== idx
            }"
            @mouseenter="hoveredIndex = idx"
            @mouseleave="hoveredIndex = -1"
          />
        </g>
        <!-- 中央动态文字 -->
        <text
          :x="size / 2"
          :y="size / 2 - 4"
          text-anchor="middle"
          font-size="22"
          font-weight="700"
          class="rm-donut__center-val"
        >{{ centerValue }}</text>
        <text
          :x="size / 2"
          :y="size / 2 + 16"
          text-anchor="middle"
          font-size="11"
          class="rm-donut__center-label"
        >{{ centerLabel }}</text>
      </svg>
    </div>

    <ul class="rm-donut__legend">
      <li
        v-for="(seg, idx) in segments"
        :key="idx"
        class="rm-donut__item"
        :class="{
          'is-active': hoveredIndex === idx,
          'is-dimmed': hoveredIndex !== -1 && hoveredIndex !== idx
        }"
        @mouseenter="hoveredIndex = idx"
        @mouseleave="hoveredIndex = -1"
      >
        <span class="rm-donut__dot" :style="{ background: seg.color }"></span>
        <span class="rm-donut__label" :title="seg.label">{{ seg.label }}</span>
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
  data() {
    return {
      hoveredIndex: -1
    };
  },
  computed: {
    radius() {
      // 预留 4px 给 hover 时的加粗描边，避免裁切
      return Math.max((this.size - this.stroke - 6) / 2, 10);
    },
    circumference() {
      return 2 * Math.PI * this.radius;
    },
    total() {
      return (this.data || []).reduce((s, d) => s + (Number(d.value) || 0), 0);
    },
    segments() {
      const palette = [
        'var(--brand-500)',
        'var(--success-500)',
        'var(--warning-500)',
        'var(--danger-500)',
        'var(--info-500)',
        'var(--purple-500)'
      ];
      let acc = 0;
      return (this.data || []).map((d, idx) => {
        const val = Number(d.value) || 0;
        const ratio = this.total ? val / this.total : 0;
        const percent = Math.round(ratio * 100);
        const color = d.color || palette[idx % palette.length];
        const length = ratio * this.circumference;
        const seg = {
          label: d.label,
          value: val,
          percent,
          color,
          length,
          offset: acc
        };
        acc += length;
        return seg;
      });
    },
    activeSegment() {
      if (this.hoveredIndex < 0 || this.hoveredIndex >= this.segments.length) {
        return null;
      }
      return this.segments[this.hoveredIndex];
    },
    centerValue() {
      return this.activeSegment ? this.activeSegment.value : this.total;
    },
    centerLabel() {
      if (this.activeSegment) {
        return `${this.activeSegment.label} · ${this.activeSegment.percent}%`;
      }
      return this.totalLabel;
    }
  }
};
</script>

<style scoped>
.rm-donut {
  display: flex;
  align-items: center;
  gap: 24px;
}

.rm-donut__chart {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
}

.rm-donut__svg {
  width: var(--size);
  height: var(--size);
  overflow: visible;
}

.rm-donut__track {
  stroke: var(--color-border);
}

.rm-donut__seg {
  cursor: pointer;
  transition: stroke-width 180ms ease, opacity 180ms ease;
}

.rm-donut__seg.is-dimmed {
  opacity: 0.4;
}

.rm-donut__center-val {
  fill: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
  pointer-events: none;
}

.rm-donut__center-label {
  fill: var(--color-text-secondary);
  pointer-events: none;
}

.rm-donut__legend {
  list-style: none;
  padding: 0;
  margin: 0;
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.rm-donut__item {
  display: grid;
  grid-template-columns: 10px minmax(0, 1fr) auto 40px;
  align-items: center;
  gap: 10px;
  padding: 7px 10px;
  border-radius: var(--radius-sm, 6px);
  font-size: 13px;
  cursor: pointer;
  border: 1px solid transparent;
  transition: background-color 150ms ease, border-color 150ms ease, opacity 150ms ease;
}

.rm-donut__item:hover,
.rm-donut__item.is-active {
  background: var(--color-bg-soft);
  border-color: var(--color-border);
}

.rm-donut__item.is-dimmed {
  opacity: 0.55;
}

.rm-donut__dot {
  width: 10px;
  height: 10px;
  border-radius: 3px;
  flex-shrink: 0;
}

.rm-donut__label {
  color: var(--color-text-primary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.rm-donut__value {
  font-weight: 600;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
  text-align: right;
}

.rm-donut__pct {
  font-size: 12px;
  color: var(--color-text-secondary);
  font-variant-numeric: tabular-nums;
  text-align: right;
}

@media (max-width: 520px) {
  .rm-donut {
    flex-direction: column;
    gap: 16px;
  }
  .rm-donut__legend {
    width: 100%;
  }
}
</style>