/**
 * CSS Lens - Content Script
 * Style Inspector & Color Picker with Shadow DOM Isolation
 */

(function () {
  // Prevent duplicate injection
  if (window.__CSS_LENS_INITIALIZED__) {
    return;
  }
  window.__CSS_LENS_INITIALIZED__ = true;

  // Extension State
  let isActive = false;
  let isFrozen = false;
  let copyFormat = 'css'; // 'css' | 'compact' | 'tailwind' | 'json'
  let currentTarget = null;
  let lastMouseX = 0;
  let lastMouseY = 0;
  let rafId = null;

  // Shadow DOM Root & Elements
  let hostEl = null;
  let shadowRoot = null;
  let overlayEl = null;
  let dimensionTagEl = null;
  let tooltipEl = null;
  let toastEl = null;

  // Cached Stylesheet
  let cssContent = `
    :host {
      all: initial;
      position: absolute;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      pointer-events: none;
      z-index: 2147483646;
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
      font-size: 13px;
      line-height: 1.4;
      box-sizing: border-box;
      -webkit-font-smoothing: antialiased;
    }
    *, *::before, *::after {
      box-sizing: border-box;
      margin: 0;
      padding: 0;
    }
    .lens-overlay {
      position: fixed;
      pointer-events: none;
      z-index: 2147483646;
      box-sizing: border-box;
      border: 2px solid #00f2fe;
      background: rgba(0, 242, 254, 0.08);
      border-radius: 4px;
      box-shadow: 0 0 0 1px rgba(0, 242, 254, 0.35), inset 0 0 14px rgba(0, 242, 254, 0.15);
      transition: all 0.05s ease-out;
      display: none;
    }
    .lens-overlay.frozen {
      border-color: #f59e0b;
      background: rgba(245, 158, 11, 0.12);
      box-shadow: 0 0 0 1px rgba(245, 158, 11, 0.45), inset 0 0 14px rgba(245, 158, 11, 0.2);
    }
    .lens-overlay.visible {
      display: block;
    }
    .lens-dimension-tag {
      position: absolute;
      top: -24px;
      left: 0;
      background: #0f172a;
      color: #38bdf8;
      border: 1px solid rgba(56, 189, 248, 0.4);
      font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
      font-size: 11px;
      font-weight: 600;
      padding: 2px 6px;
      border-radius: 4px;
      white-space: nowrap;
      pointer-events: none;
      box-shadow: 0 4px 10px rgba(0, 0, 0, 0.4);
    }
    .lens-dimension-tag.flip-bottom {
      top: auto;
      bottom: -24px;
    }
    .lens-tooltip {
      position: fixed;
      z-index: 2147483647;
      pointer-events: none;
      width: 320px;
      max-width: calc(100vw - 32px);
      background: rgba(15, 23, 42, 0.94);
      backdrop-filter: blur(16px);
      -webkit-backdrop-filter: blur(16px);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 12px;
      box-shadow: 0 20px 40px -8px rgba(0, 0, 0, 0.65), 0 0 0 1px rgba(255, 255, 255, 0.06);
      color: #f1f5f9;
      padding: 12px 14px;
      display: none;
      flex-direction: column;
      gap: 10px;
      transform: translate(0, 0);
      transition: opacity 0.1s ease;
      opacity: 0;
    }
    .lens-tooltip.visible {
      display: flex;
      opacity: 1;
    }
    .lens-tooltip.frozen-mode {
      border-color: rgba(245, 158, 11, 0.5);
      box-shadow: 0 20px 40px -8px rgba(0, 0, 0, 0.7), 0 0 16px rgba(245, 158, 11, 0.25);
    }
    .lens-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 8px;
      gap: 8px;
    }
    .lens-element-selector {
      display: flex;
      align-items: baseline;
      gap: 4px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
    }
    .lens-tag {
      color: #38bdf8;
      font-weight: 700;
      font-size: 13px;
    }
    .lens-class-id {
      color: #94a3b8;
      font-size: 11px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .lens-format-badge {
      background: rgba(56, 189, 248, 0.15);
      color: #38bdf8;
      border: 1px solid rgba(56, 189, 248, 0.3);
      font-size: 10px;
      font-weight: 700;
      padding: 1px 6px;
      border-radius: 999px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
      flex-shrink: 0;
    }
    .lens-section {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .lens-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 12px;
      gap: 8px;
    }
    .lens-label {
      color: #94a3b8;
      font-size: 11px;
      font-weight: 500;
      display: flex;
      align-items: center;
      gap: 5px;
      flex-shrink: 0;
    }
    .lens-value {
      font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
      font-size: 11.5px;
      color: #f8fafc;
      font-weight: 500;
      text-align: right;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .lens-color-pill {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: rgba(255, 255, 255, 0.06);
      padding: 2px 6px 2px 4px;
      border-radius: 6px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      font-family: 'JetBrains Mono', Consolas, Menlo, monospace;
      font-size: 11px;
    }
    .lens-swatch {
      width: 12px;
      height: 12px;
      border-radius: 3px;
      border: 1px solid rgba(255, 255, 255, 0.3);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.3);
      flex-shrink: 0;
    }
    .lens-meta-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6px;
      background: rgba(255, 255, 255, 0.03);
      border-radius: 8px;
      padding: 6px 8px;
      border: 1px solid rgba(255, 255, 255, 0.05);
    }
    .lens-meta-item {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .lens-meta-label {
      font-size: 10px;
      color: #64748b;
      text-transform: uppercase;
      font-weight: 600;
      letter-spacing: 0.4px;
    }
    .lens-meta-value {
      font-family: 'JetBrains Mono', Consolas, monospace;
      font-size: 11px;
      color: #e2e8f0;
      font-weight: 500;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .lens-footer {
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding-top: 8px;
      display: flex;
      align-items: center;
      justify-content: space-between;
      font-size: 10.5px;
      color: #94a3b8;
    }
    .lens-hotkey-group {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .lens-kbd {
      background: rgba(255, 255, 255, 0.1);
      color: #e2e8f0;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 4px;
      padding: 1px 4px;
      font-family: 'JetBrains Mono', monospace;
      font-size: 9.5px;
      font-weight: 600;
    }
    .lens-status-badge {
      color: #10b981;
      font-weight: 600;
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .lens-status-badge.frozen {
      color: #f59e0b;
    }
    .lens-status-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: currentColor;
      box-shadow: 0 0 8px currentColor;
    }
    .lens-toast {
      position: fixed;
      bottom: 28px;
      left: 50%;
      transform: translateX(-50%) translateY(40px);
      background: #0f172a;
      color: #ffffff;
      border: 1px solid #10b981;
      box-shadow: 0 16px 36px rgba(0, 0, 0, 0.6), 0 0 20px rgba(16, 185, 129, 0.25);
      padding: 10px 18px;
      border-radius: 10px;
      display: flex;
      align-items: center;
      gap: 10px;
      z-index: 2147483647;
      pointer-events: none;
      opacity: 0;
      transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1);
      font-size: 13px;
      font-weight: 500;
    }
    .lens-toast.show {
      transform: translateX(-50%) translateY(0);
      opacity: 1;
    }
    .lens-toast-icon {
      background: #10b981;
      color: #0f172a;
      width: 20px;
      height: 20px;
      border-radius: 50%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 900;
      font-size: 12px;
    }
    .lens-toast-preview {
      font-family: 'JetBrains Mono', monospace;
      font-size: 11px;
      color: #6ee7b7;
      max-width: 260px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  `;

  /**
   * Initialize Shadow DOM UI container
   */
  function initShadowDOM() {
    if (hostEl && shadowRoot) return;

    hostEl = document.createElement('div');
    hostEl.id = 'css-lens-root';
    hostEl.style.cssText = 'all: initial; position: absolute; top: 0; left: 0; pointer-events: none; z-index: 2147483646;';

    shadowRoot = hostEl.attachShadow({ mode: 'open' });

    // Inject Styles
    const styleEl = document.createElement('style');
    styleEl.textContent = cssContent;
    shadowRoot.appendChild(styleEl);

    // Overlay Element
    overlayEl = document.createElement('div');
    overlayEl.className = 'lens-overlay';
    dimensionTagEl = document.createElement('div');
    dimensionTagEl.className = 'lens-dimension-tag';
    dimensionTagEl.textContent = '0 × 0 px';
    overlayEl.appendChild(dimensionTagEl);
    shadowRoot.appendChild(overlayEl);

    // Tooltip Element
    tooltipEl = document.createElement('div');
    tooltipEl.className = 'lens-tooltip';
    tooltipEl.innerHTML = `
      <div class="lens-header">
        <div class="lens-element-selector">
          <span class="lens-tag" id="lens-el-tag">div</span>
          <span class="lens-class-id" id="lens-el-id-class"></span>
        </div>
        <div class="lens-format-badge" id="lens-format-label">CSS</div>
      </div>
      <div class="lens-section">
        <div class="lens-row">
          <span class="lens-label">Font Family</span>
          <span class="lens-value" id="lens-val-font">Inter, sans-serif</span>
        </div>
        <div class="lens-row">
          <span class="lens-label">Size & Weight</span>
          <span class="lens-value" id="lens-val-size-weight">16px • 400</span>
        </div>
        <div class="lens-row">
          <span class="lens-label">Line Height</span>
          <span class="lens-value" id="lens-val-lh">24px (1.5)</span>
        </div>
      </div>
      <div class="lens-section">
        <div class="lens-row">
          <span class="lens-label">Color</span>
          <div class="lens-color-pill">
            <span class="lens-swatch" id="lens-swatch-color"></span>
            <span id="lens-val-color">#ffffff</span>
          </div>
        </div>
        <div class="lens-row">
          <span class="lens-label">Background</span>
          <div class="lens-color-pill">
            <span class="lens-swatch" id="lens-swatch-bg"></span>
            <span id="lens-val-bg">#0f172a</span>
          </div>
        </div>
      </div>
      <div class="lens-meta-grid">
        <div class="lens-meta-item">
          <span class="lens-meta-label">Display</span>
          <span class="lens-meta-value" id="lens-val-display">block</span>
        </div>
        <div class="lens-meta-item">
          <span class="lens-meta-label">Padding</span>
          <span class="lens-meta-value" id="lens-val-padding">0px</span>
        </div>
        <div class="lens-meta-item">
          <span class="lens-meta-label">Margin</span>
          <span class="lens-meta-value" id="lens-val-margin">0px</span>
        </div>
        <div class="lens-meta-item">
          <span class="lens-meta-label">Radius</span>
          <span class="lens-meta-value" id="lens-val-radius">0px</span>
        </div>
      </div>
      <div class="lens-footer">
        <div class="lens-hotkey-group">
          <span><span class="lens-kbd">Click</span> Copy</span>
          <span><span class="lens-kbd">Space</span> Freeze</span>
          <span><span class="lens-kbd">Esc</span> Exit</span>
        </div>
        <div class="lens-status-badge" id="lens-live-status">
          <span class="lens-status-dot"></span>
          <span id="lens-status-text">LIVE</span>
        </div>
      </div>
    `;
    shadowRoot.appendChild(tooltipEl);

    // Toast Element
    toastEl = document.createElement('div');
    toastEl.className = 'lens-toast';
    toastEl.innerHTML = `
      <div class="lens-toast-icon">✓</div>
      <div>
        <div id="lens-toast-title">Copied to Clipboard!</div>
        <div class="lens-toast-preview" id="lens-toast-snippet">font-family: ...</div>
      </div>
    `;
    shadowRoot.appendChild(toastEl);

    document.documentElement.appendChild(hostEl);
  }

  // Load preferences from storage
  if (chrome.storage && chrome.storage.local) {
    chrome.storage.local.get(['cssLensFormat'], (res) => {
      if (res.cssLensFormat) {
        copyFormat = res.cssLensFormat;
      }
    });
  }

  /**
   * Color Parsing & Conversion Utilities
   */
  function rgbToHex(rgbaStr) {
    if (!rgbaStr || rgbaStr === 'transparent' || rgbaStr === 'rgba(0, 0, 0, 0)') {
      return 'transparent';
    }

    const match = rgbaStr.match(/rgba?\((\d+),\s*(\d+),\s*(\d+)(?:,\s*([\d.]+))?\)/);
    if (!match) return rgbaStr;

    const r = parseInt(match[1], 10);
    const g = parseInt(match[2], 10);
    const b = parseInt(match[3], 10);
    const a = match[4] !== undefined ? parseFloat(match[4]) : 1;

    const toHex2 = (n) => n.toString(16).padStart(2, '0').toUpperCase();
    if (a < 1 && a >= 0) {
      const alphaHex = Math.round(a * 255).toString(16).padStart(2, '0').toUpperCase();
      return `#${toHex2(r)}${toHex2(g)}${toHex2(b)}${alphaHex}`;
    }
    return `#${toHex2(r)}${toHex2(g)}${toHex2(b)}`;
  }

  function getEffectiveBackgroundColor(el) {
    let current = el;
    while (current && current !== document) {
      const style = window.getComputedStyle(current);
      const bg = style.backgroundColor;
      if (bg && bg !== 'transparent' && bg !== 'rgba(0, 0, 0, 0)') {
        return bg;
      }
      current = current.parentElement;
    }
    return 'rgb(255, 255, 255)'; // Default fallback
  }

  function getFontWeightName(weight) {
    const w = parseInt(weight, 10);
    if (w <= 100) return 'Thin';
    if (w <= 200) return 'ExtraLight';
    if (w <= 300) return 'Light';
    if (w <= 400) return 'Regular';
    if (w <= 500) return 'Medium';
    if (w <= 600) return 'SemiBold';
    if (w <= 700) return 'Bold';
    if (w <= 800) return 'ExtraBold';
    return 'Black';
  }

  function cleanFontFamily(fontFamily) {
    if (!fontFamily) return 'inherit';
    const fonts = fontFamily.split(',').map((f) => f.trim().replace(/^['"]|['"]$/g, ''));
    if (fonts.length === 1) return fonts[0];
    return `${fonts[0]}, ${fonts[fonts.length - 1]}`;
  }

  /**
   * Extract comprehensive styles from element
   */
  function extractElementData(el) {
    if (!el || el === hostEl || hostEl.contains(el)) return null;

    const rect = el.getBoundingClientRect();
    const style = window.getComputedStyle(el);

    const tagName = el.tagName.toLowerCase();
    const id = el.id ? `#${el.id}` : '';
    const classList = Array.from(el.classList).filter(c => !c.startsWith('css-lens')).slice(0, 3).map(c => `.${c}`).join('');
    const selector = `${id}${classList}`;

    const color = style.color;
    const colorHex = rgbToHex(color);

    const effectiveBg = getEffectiveBackgroundColor(el);
    const bgHex = rgbToHex(effectiveBg);

    const fontFamilyClean = cleanFontFamily(style.fontFamily);
    const fontSize = style.fontSize;
    const fontWeight = style.fontWeight;
    const fontWeightLabel = `${fontWeight} • ${getFontWeightName(fontWeight)}`;

    const lineHeight = style.lineHeight;
    let lhFormatted = lineHeight;
    if (lineHeight !== 'normal' && fontSize) {
      const fsNum = parseFloat(fontSize);
      const lhNum = parseFloat(lineHeight);
      if (fsNum > 0 && lhNum > 0) {
        lhFormatted = `${lineHeight} (${(lhNum / fsNum).toFixed(1)})`;
      }
    }

    return {
      element: el,
      rect,
      tag: tagName,
      selector,
      dimensions: `${Math.round(rect.width)} × ${Math.round(rect.height)} px`,
      fontFamily: fontFamilyClean,
      fullFontFamily: style.fontFamily,
      fontSize,
      fontWeight,
      fontWeightLabel,
      lineHeight,
      lhFormatted,
      letterSpacing: style.letterSpacing,
      color,
      colorHex,
      bg: effectiveBg,
      bgHex,
      display: style.display,
      padding: style.padding,
      margin: style.margin,
      borderRadius: style.borderRadius,
      boxShadow: style.boxShadow !== 'none' ? 'present' : 'none',
      fullBoxShadow: style.boxShadow
    };
  }

  /**
   * Update Tooltip and Overlay UI
   */
  function updateUI(data, mouseX, mouseY) {
    if (!data) return;

    // 1. Overlay update
    const rect = data.rect;
    overlayEl.style.top = `${rect.top}px`;
    overlayEl.style.left = `${rect.left}px`;
    overlayEl.style.width = `${rect.width}px`;
    overlayEl.style.height = `${rect.height}px`;
    overlayEl.classList.add('visible');

    if (isFrozen) {
      overlayEl.classList.add('frozen');
    } else {
      overlayEl.classList.remove('frozen');
    }

    dimensionTagEl.textContent = data.dimensions;
    if (rect.top < 30) {
      dimensionTagEl.classList.add('flip-bottom');
    } else {
      dimensionTagEl.classList.remove('flip-bottom');
    }

    // 2. Populate Tooltip Data
    shadowRoot.getElementById('lens-el-tag').textContent = data.tag;
    shadowRoot.getElementById('lens-el-id-class').textContent = data.selector;
    shadowRoot.getElementById('lens-format-label').textContent = copyFormat.toUpperCase();

    shadowRoot.getElementById('lens-val-font').textContent = data.fontFamily;
    shadowRoot.getElementById('lens-val-size-weight').textContent = `${data.fontSize} • ${data.fontWeightLabel}`;
    shadowRoot.getElementById('lens-val-lh').textContent = data.lhFormatted;

    // Color swatches
    shadowRoot.getElementById('lens-swatch-color').style.backgroundColor = data.color;
    shadowRoot.getElementById('lens-val-color').textContent = data.colorHex;

    shadowRoot.getElementById('lens-swatch-bg').style.backgroundColor = data.bg;
    shadowRoot.getElementById('lens-val-bg').textContent = data.bgHex;

    shadowRoot.getElementById('lens-val-display').textContent = data.display;
    shadowRoot.getElementById('lens-val-padding').textContent = data.padding;
    shadowRoot.getElementById('lens-val-margin').textContent = data.margin;
    shadowRoot.getElementById('lens-val-radius').textContent = data.borderRadius;

    // Status
    const statusText = shadowRoot.getElementById('lens-status-text');
    const statusBadge = shadowRoot.getElementById('lens-live-status');
    if (isFrozen) {
      statusText.textContent = 'FROZEN';
      statusBadge.classList.add('frozen');
      tooltipEl.classList.add('frozen-mode');
    } else {
      statusText.textContent = 'LIVE';
      statusBadge.classList.remove('frozen');
      tooltipEl.classList.remove('frozen-mode');
    }

    // 3. Smart Tooltip Positioning (never clip outside viewport)
    tooltipEl.classList.add('visible');
    const tooltipWidth = 320;
    const tooltipHeight = 290;
    const margin = 16;

    let posX = mouseX + margin;
    let posY = mouseY + margin;

    // Flip horizontally if overflow right
    if (posX + tooltipWidth > window.innerWidth - 12) {
      posX = mouseX - tooltipWidth - margin;
    }
    if (posX < 12) posX = 12;

    // Flip vertically if overflow bottom
    if (posY + tooltipHeight > window.innerHeight - 12) {
      posY = mouseY - tooltipHeight - margin;
    }
    if (posY < 12) posY = 12;

    tooltipEl.style.left = `${posX}px`;
    tooltipEl.style.top = `${posY}px`;
  }

  /**
   * Format CSS snippet based on selected style format
   */
  function formatCssSnippet(data, format) {
    if (!data) return '';

    switch (format) {
      case 'compact':
        return `font-family: ${data.fullFontFamily}; font-size: ${data.fontSize}; font-weight: ${data.fontWeight}; line-height: ${data.lineHeight}; color: ${data.colorHex}; background: ${data.bgHex}; border-radius: ${data.borderRadius};`;

      case 'tailwind': {
        const parts = [];
        parts.push(`font-[${data.fontFamily.split(',')[0].replace(/\s+/g, '_')}]`);
        parts.push(`text-[${data.fontSize}]`);
        const wMap = { '400': 'font-normal', '500': 'font-medium', '600': 'font-semibold', '700': 'font-bold', '800': 'font-extrabold' };
        parts.push(wMap[data.fontWeight] || `font-[${data.fontWeight}]`);
        if (data.colorHex !== 'transparent') parts.push(`text-[${data.colorHex}]`);
        if (data.bgHex !== 'transparent') parts.push(`bg-[${data.bgHex}]`);
        if (data.borderRadius && data.borderRadius !== '0px') parts.push(`rounded-[${data.borderRadius.replace(/\s+/g, '_')}]`);
        if (data.padding && data.padding !== '0px') parts.push(`p-[${data.padding.replace(/\s+/g, '_')}]`);
        return parts.join(' ');
      }

      case 'json':
        return JSON.stringify({
          tag: data.tag,
          selector: data.selector,
          fontFamily: data.fullFontFamily,
          fontSize: data.fontSize,
          fontWeight: data.fontWeight,
          lineHeight: data.lineHeight,
          color: data.colorHex,
          backgroundColor: data.bgHex,
          display: data.display,
          padding: data.padding,
          margin: data.margin,
          borderRadius: data.borderRadius,
          boxShadow: data.fullBoxShadow
        }, null, 2);

      case 'css':
      default:
        return [
          `/* <${data.tag}${data.selector}> Styles */`,
          `font-family: ${data.fullFontFamily};`,
          `font-size: ${data.fontSize};`,
          `font-weight: ${data.fontWeight};`,
          `line-height: ${data.lineHeight};`,
          `letter-spacing: ${data.letterSpacing};`,
          `color: ${data.colorHex};`,
          `background-color: ${data.bgHex};`,
          `display: ${data.display};`,
          `padding: ${data.padding};`,
          `margin: ${data.margin};`,
          `border-radius: ${data.borderRadius};`,
          data.fullBoxShadow !== 'none' ? `box-shadow: ${data.fullBoxShadow};` : null
        ].filter(Boolean).join('\n');
    }
  }

  /**
   * Show animated Toast Notification
   */
  let toastTimer = null;
  function showToast(formatName, snippet) {
    if (!toastEl) return;

    clearTimeout(toastTimer);
    shadowRoot.getElementById('lens-toast-title').textContent = `Copied ${formatName.toUpperCase()} to Clipboard! 📋`;
    shadowRoot.getElementById('lens-toast-snippet').textContent = snippet.split('\n')[0];

    toastEl.classList.add('show');
    toastTimer = setTimeout(() => {
      toastEl.classList.remove('show');
    }, 2400);
  }

  /**
   * Copy current inspected element styles
   */
  async function copyStyles() {
    if (!currentTarget) return;

    const data = extractElementData(currentTarget);
    if (!data) return;

    const snippet = formatCssSnippet(data, copyFormat);

    try {
      if (navigator.clipboard && navigator.clipboard.writeText) {
        await navigator.clipboard.writeText(snippet);
      } else {
        const textarea = document.createElement('textarea');
        textarea.value = snippet;
        document.body.appendChild(textarea);
        textarea.select();
        document.execCommand('copy');
        textarea.remove();
      }

      showToast(copyFormat, snippet);

      // Save to background history
      try {
        chrome.runtime.sendMessage({
          type: 'SAVE_HISTORY_ITEM',
          item: {
            tag: data.tag,
            classes: data.selector,
            color: data.colorHex,
            bg: data.bgHex,
            font: data.fontFamily,
            snippet,
            format: copyFormat
          }
        });
      } catch (e) {
        // Background might be dormant
      }
    } catch (err) {
      console.error('[CSS Lens] Copy failed:', err);
    }
  }

  /**
   * Event Handlers
   */
  function onMouseMove(e) {
    if (!isActive) return;

    lastMouseX = e.clientX;
    lastMouseY = e.clientY;

    if (isFrozen) {
      // In frozen mode, only update tooltip position if user moves mouse, keeping target locked
      if (currentTarget) {
        const data = extractElementData(currentTarget);
        updateUI(data, lastMouseX, lastMouseY);
      }
      return;
    }

    if (rafId) cancelAnimationFrame(rafId);

    rafId = requestAnimationFrame(() => {
      // Temporarily hide our overlay so document.elementFromPoint picks genuine page element
      if (hostEl) hostEl.style.display = 'none';
      const el = document.elementFromPoint(e.clientX, e.clientY);
      if (hostEl) hostEl.style.display = 'block';

      if (!el || el === document.documentElement || el === document.body) {
        if (el === document.body) {
          currentTarget = el;
          const data = extractElementData(el);
          updateUI(data, e.clientX, e.clientY);
        }
        return;
      }

      currentTarget = el;
      const data = extractElementData(el);
      if (data) {
        updateUI(data, e.clientX, e.clientY);
      }
    });
  }

  function onClick(e) {
    if (!isActive) return;

    e.preventDefault();
    e.stopPropagation();
    e.stopImmediatePropagation();

    copyStyles();
  }

  function onKeyDown(e) {
    if (!isActive) return;

    // Esc: Turn off Lens
    if (e.key === 'Escape') {
      e.preventDefault();
      deactivateLens();
      return;
    }

    // Space or Alt: Toggle Freeze Mode
    if (e.code === 'Space' || e.key === 'Alt') {
      e.preventDefault();
      isFrozen = !isFrozen;
      if (currentTarget) {
        const data = extractElementData(currentTarget);
        updateUI(data, lastMouseX, lastMouseY);
      }
      return;
    }

    // C: Quick Copy
    if (e.key === 'c' || e.key === 'C') {
      // Avoid intercepting if typing inside an editable field when frozen
      if (document.activeElement && ['input', 'textarea'].includes(document.activeElement.tagName.toLowerCase())) {
        return;
      }
      e.preventDefault();
      copyStyles();
    }
  }

  function onScroll() {
    if (!isActive || !currentTarget) return;
    const data = extractElementData(currentTarget);
    if (data) {
      updateUI(data, lastMouseX, lastMouseY);
    }
  }

  /**
   * Activation / Deactivation
   */
  function activateLens() {
    if (isActive) return;
    isActive = true;
    isFrozen = false;
    initShadowDOM();

    window.addEventListener('mousemove', onMouseMove, { capture: true, passive: true });
    window.addEventListener('click', onClick, { capture: true });
    window.addEventListener('keydown', onKeyDown, { capture: true });
    window.addEventListener('scroll', onScroll, { capture: true, passive: true });

    try {
      chrome.runtime.sendMessage({ type: 'STATE_CHANGED', active: true });
    } catch {}

    // Trigger initial scan under current mouse position
    if (lastMouseX && lastMouseY) {
      const el = document.elementFromPoint(lastMouseX, lastMouseY);
      if (el) {
        currentTarget = el;
        const data = extractElementData(el);
        if (data) updateUI(data, lastMouseX, lastMouseY);
      }
    }
  }

  function deactivateLens() {
    if (!isActive) return;
    isActive = false;
    isFrozen = false;
    currentTarget = null;

    window.removeEventListener('mousemove', onMouseMove, { capture: true });
    window.removeEventListener('click', onClick, { capture: true });
    window.removeEventListener('keydown', onKeyDown, { capture: true });
    window.removeEventListener('scroll', onScroll, { capture: true });

    if (overlayEl) overlayEl.classList.remove('visible', 'frozen');
    if (tooltipEl) tooltipEl.classList.remove('visible', 'frozen-mode');
    if (toastEl) toastEl.classList.remove('show');

    try {
      chrome.runtime.sendMessage({ type: 'STATE_CHANGED', active: false });
    } catch {}
  }

  /**
   * Message Listener from Popup or Background
   */
  chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
    if (message.type === 'TOGGLE_CSS_LENS') {
      const targetState = message.state !== undefined ? message.state : !isActive;
      if (targetState) {
        activateLens();
      } else {
        deactivateLens();
      }
      sendResponse({ active: isActive, format: copyFormat, isFrozen });
      return true;
    }

    if (message.type === 'GET_STATUS') {
      sendResponse({ active: isActive, format: copyFormat, isFrozen });
      return true;
    }

    if (message.type === 'SET_FORMAT') {
      copyFormat = message.format || 'css';
      if (chrome.storage && chrome.storage.local) {
        chrome.storage.local.set({ cssLensFormat: copyFormat });
      }
      if (isActive && currentTarget) {
        const data = extractElementData(currentTarget);
        if (data) updateUI(data, lastMouseX, lastMouseY);
      }
      sendResponse({ success: true, format: copyFormat });
      return true;
    }
  });
})();
