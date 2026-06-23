from __future__ import annotations


def telemetry_viewer_html(initial_session_id: str | None = None) -> str:
    safe_session_id = (initial_session_id or "").replace("\\", "").replace("'", "")
    html = """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Telemetry Viewer</title>
  <style>
    :root { color-scheme: light dark; font-family: Segoe UI, Arial, sans-serif; }
    body { margin: 22px; background: Canvas; color: CanvasText; }
    header { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: end; gap: 12px; }
    h1 { margin: 0; font-size: 24px; }
    h2 { margin: 22px 0 8px; font-size: 17px; }
    nav a { color: LinkText; margin-left: 14px; }
    label { display: inline-flex; flex-direction: column; gap: 4px; margin: 8px 12px 8px 0; font-size: 12px; text-transform: uppercase; opacity: .82; }
    select, input[type=file] { min-width: 250px; padding: 5px 7px; text-transform: none; }
    button { padding: 6px 10px; margin: 8px 8px 8px 0; }
    .status { margin-top: 8px; opacity: .78; }
    .meta { display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 8px 16px; margin: 16px 0; }
    .metric { padding: 8px 0; border-bottom: 1px solid color-mix(in srgb, CanvasText 18%, transparent); }
    .metric strong { display: block; font-size: 12px; text-transform: uppercase; opacity: .72; }
    .chart { margin: 20px 0 30px; border-top: 1px solid color-mix(in srgb, CanvasText 20%, transparent); padding-top: 12px; }
    .chart-controls { display: flex; flex-wrap: wrap; gap: 8px 12px; align-items: end; }
    canvas { width: 100%; height: 260px; border-bottom: 1px solid color-mix(in srgb, CanvasText 25%, transparent); }
    .legend { font-size: 13px; opacity: .85; margin-top: 4px; }
    .empty { opacity: .75; padding: 20px 0; }
    .tooltip { position: fixed; z-index: 20; display: none; max-width: 340px; padding: 8px 10px; border: 1px solid color-mix(in srgb, CanvasText 28%, transparent); background: Canvas; color: CanvasText; box-shadow: 0 8px 18px color-mix(in srgb, CanvasText 18%, transparent); font-size: 12px; pointer-events: none; white-space: pre-line; }
  </style>
</head>
<body>
  <header>
    <div><h1>Telemetry Viewer</h1><div class="status" id="status">Loading...</div></div>
    <nav><a href="/history">History</a><a href="/poc">Dashboard</a><a href="/telemetry">Viewer</a></nav>
  </header>

  <section>
    <label>Stored session <select id="recordingSelect"></select></label>
    <button id="loadSelected">Load</button>
    <label>X axis <select id="xMode"><option value="time" selected>Time</option><option value="track">Track %</option><option value="sample">Sample</option></select></label>
    <label>Open file <input id="fileInput" type="file" accept=".json,.jsonl,application/json"></label>
  </section>
  <section class="meta" id="meta"></section>
  <section id="charts"></section>
  <div class="tooltip" id="tooltip"></div>

  <script>
    const initialSessionId = '__SESSION_ID__';
    const statusEl = document.getElementById('status');
    const recordingSelect = document.getElementById('recordingSelect');
    const loadSelected = document.getElementById('loadSelected');
    const fileInput = document.getElementById('fileInput');
    const xMode = document.getElementById('xMode');
    const metaEl = document.getElementById('meta');
    const chartsEl = document.getElementById('charts');
    const tooltipEl = document.getElementById('tooltip');
    let report = null;
    let defaultA = '';
    let defaultB = '';

    function fmt(value) {
      if (value === null || value === undefined || value === '') return '-';
      if (typeof value === 'number') return Number.isInteger(value) ? String(value) : String(Number(value.toFixed(3)));
      return String(value);
    }

    function fmtTime(value) {
      if (value === null || value === undefined || value === '') return '-';
      const number = Number(value);
      if (!Number.isFinite(number)) return '-';
      const minutes = Math.floor(number / 60);
      const seconds = (number % 60).toFixed(3).padStart(6, '0');
      return `${minutes}:${seconds}`;
    }

    function metric(label, value) {
      const div = document.createElement('div');
      div.className = 'metric';
      const strong = document.createElement('strong');
      strong.textContent = label;
      div.appendChild(strong);
      div.appendChild(document.createTextNode(fmt(value)));
      metaEl.appendChild(div);
    }

    function allLaps() {
      return (report && (report.all_laps || report.laps)) || [];
    }

    function normalizeReport(payload) {
      const normalized = payload || {};
      const laps = (normalized.all_laps || normalized.laps || []).map((lap, index) => normalizeLap(lap, index));
      normalized.all_laps = sortLaps(laps);
      normalized.laps = sortLaps((normalized.laps || []).map((lap, index) => normalizeLap(lap, index)));
      if (!normalized.laps.length) normalized.laps = normalized.all_laps.filter(lap => lap.eligible_for_report);
      markFastestLaps(normalized.all_laps);
      markFastestLaps(normalized.laps);
      if (!normalized.reference_lap) {
        const fastest = normalized.all_laps.find(lap => lap.is_fastest_overall);
        if (fastest) normalized.reference_lap = {
          driver_id: fastest.driver_id,
          driver_name: fastest.driver_name,
          lap_number: fastest.lap_number,
          lap_time: fastest.lap_time,
        };
      }
      normalized.proper_lap_count = normalized.proper_lap_count ?? normalized.all_laps.filter(lap => lap.eligible_for_report).length;
      normalized.excluded_lap_count = normalized.excluded_lap_count ?? Math.max(0, normalized.all_laps.length - normalized.proper_lap_count);
      return normalized;
    }

    function normalizeLap(lap, index) {
      const normalized = { ...lap };
      normalized.lap_id = normalized.lap_id || `${normalized.driver_id ?? normalized.driver_name ?? 'driver'}:${normalized.lap_number ?? index}:${index}`;
      normalized.series = normalized.series || {};
      normalized.sample_count = normalized.sample_count ?? Math.max(0, ...Object.values(normalized.series).map(values => Array.isArray(values) ? values.length : 0));
      normalized.lap_time = normalized.lap_time ?? derivedLapTime(normalized);
      normalized.lap_classification = normalized.lap_classification || (normalized.eligible_for_report ? 'proper' : 'uploaded');
      return normalized;
    }

    function derivedLapTime(lap) {
      const times = ((lap.series || {}).time_seconds || []).map(value => Number(value)).filter(Number.isFinite);
      if (times.length < 2) return null;
      const start = times.find(value => value >= 0);
      const end = times[times.length - 1];
      if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) return null;
      return Number((end - start).toFixed(3));
    }

    function sortLaps(laps) {
      return [...laps].sort((left, right) => {
        const driverCompare = String(left.driver_name || left.driver_id || '').localeCompare(String(right.driver_name || right.driver_id || ''), undefined, { numeric: true });
        if (driverCompare) return driverCompare;
        return Number(left.lap_number || 0) - Number(right.lap_number || 0);
      });
    }

    function markFastestLaps(laps) {
      if (!laps.length) return;
      const candidates = laps.filter(lap => Number.isFinite(Number(lap.lap_time)) && (lap.eligible_for_report || !laps.some(item => item.eligible_for_report)));
      if (!candidates.length) return;
      const overall = candidates.reduce((best, lap) => Number(lap.lap_time) < Number(best.lap_time) ? lap : best, candidates[0]);
      overall.is_fastest_overall = true;
      const byDriver = new Map();
      for (const lap of candidates) {
        const key = String(lap.driver_id ?? lap.driver_name ?? 'driver');
        const previous = byDriver.get(key);
        if (!previous || Number(lap.lap_time) < Number(previous.lap_time)) byDriver.set(key, lap);
      }
      for (const lap of byDriver.values()) lap.is_fastest_personal = true;
    }

    function lapLabel(lap) {
      const tags = [];
      if (lap.is_fastest_overall) tags.push('Fastest Ovrl.');
      else if (lap.is_fastest_personal) tags.push('Fastest Pers.');
      const tagText = tags.length ? ` (${tags.join(', ')})` : '';
      const kind = lap.lap_classification || (lap.eligible_for_report ? 'proper' : 'partial');
      return `${lap.driver_name || lap.driver_id} lap ${fmt(lap.lap_number)} ${fmtTime(lap.lap_time)} ${kind}${tagText}`;
    }

    function noneOption() {
      const option = document.createElement('option');
      option.value = '';
      option.textContent = 'None';
      return option;
    }

    function optionForLap(lap) {
      const option = document.createElement('option');
      option.value = lap.lap_id;
      option.textContent = lapLabel(lap);
      return option;
    }

    async function loadRecordings() {
      try {
        const response = await fetch('/api/recordings', { cache: 'no-store' });
        const payload = await response.json();
        recordingSelect.replaceChildren();
        for (const recording of payload.recordings || []) {
          const option = document.createElement('option');
          option.value = recording.session_id;
          option.textContent = `${recording.session_id}  ${fmt(recording.track)} ${fmt(recording.session_type)} status=${fmt(recording.status)}`;
          recordingSelect.appendChild(option);
        }
      } catch (error) {
        statusEl.textContent = `error=${error}`;
      }
    }

    async function loadReport(sessionId) {
      if (!sessionId) return;
      statusEl.textContent = `Loading ${sessionId}...`;
      const response = await fetch(`/api/reports/${encodeURIComponent(sessionId)}`, { cache: 'no-store' });
      if (!response.ok) throw new Error(`${response.status} ${response.statusText}`);
      renderReport(await response.json());
    }

    function renderReport(payload) {
      report = normalizeReport(payload);
      const laps = allLaps();
      const proper = laps.filter(lap => lap.eligible_for_report);
      const reference = laps.find(lap => lap.is_fastest_overall) || proper[0] || laps[0];
      defaultA = reference ? reference.lap_id : '';
      defaultB = '';
      statusEl.textContent = `${fmt(report.track)} ${fmt(report.session_type)} status=${fmt(report.status)}`;
      metaEl.replaceChildren();
      metric('Session', report.session_id);
      metric('Proper laps', report.proper_lap_count);
      metric('Other laps', report.excluded_lap_count);
      metric('Samples stored', report.telemetry_sample_count);
      metric('Reference', report.reference_lap ? `${report.reference_lap.driver_name} lap ${report.reference_lap.lap_number} ${fmtTime(report.reference_lap.lap_time)}` : '-');
      chartsEl.replaceChildren();
      if (!laps.length) {
        chartsEl.className = 'empty';
        chartsEl.textContent = 'No telemetry laps were available.';
        if (report.status === 'building' && report.session_id) {
          chartsEl.textContent = 'Telemetry report is still being generated.';
          setTimeout(() => loadReport(report.session_id).catch(error => { statusEl.textContent = `error=${error}`; }), 1000);
        }
        return;
      }
      chartsEl.className = '';
      for (const channel of (report.channels || []).filter(item => item.key !== 'delta_time')) {
        chartsEl.appendChild(chartElement(channel));
      }
    }

    function chartElement(channel) {
      const wrapper = document.createElement('div');
      wrapper.className = 'chart';
      const heading = document.createElement('h2');
      heading.textContent = `${channel.label} ${channel.unit || ''}`;
      wrapper.appendChild(heading);
      const controls = document.createElement('div');
      controls.className = 'chart-controls';
      const selectA = lapSelect(defaultA, false);
      const selectB = lapSelect(defaultB, true);
      controls.appendChild(labeled('Driver A', selectA));
      controls.appendChild(labeled('Driver B', selectB));
      wrapper.appendChild(controls);
      const canvas = document.createElement('canvas');
      wrapper.appendChild(canvas);
      const legend = document.createElement('div');
      legend.className = 'legend';
      wrapper.appendChild(legend);
      const redraw = () => drawChart(canvas, channel, selectA.value, selectB.value, legend);
      selectA.onchange = redraw;
      selectB.onchange = redraw;
      xMode.addEventListener('change', redraw);
      canvas.onmousemove = showTooltip;
      canvas.onmouseleave = hideTooltip;
      setTimeout(redraw, 0);
      return wrapper;
    }

    function labeled(labelText, input) {
      const label = document.createElement('label');
      label.textContent = labelText;
      label.appendChild(input);
      return label;
    }

    function lapSelect(selectedValue, allowNone) {
      const select = document.createElement('select');
      if (allowNone) select.appendChild(noneOption());
      for (const lap of allLaps()) select.appendChild(optionForLap(lap));
      select.value = selectedValue;
      return select;
    }

    function findLap(lapId) {
      return allLaps().find(lap => lap.lap_id === lapId) || null;
    }

    function drawChart(canvas, channel, lapAId, lapBId, legend) {
      const lapA = findLap(lapAId);
      const lapB = findLap(lapBId);
      const seriesA = lapA ? valuesFor(lapA, channel.key) : [];
      const seriesB = lapB ? valuesFor(lapB, channel.key) : [];
      const xA = lapA ? xValues(lapA, seriesA.length) : [];
      const xB = lapB ? xValues(lapB, seriesB.length) : [];
      const values = [...seriesA, ...seriesB].filter(value => typeof value === 'number' && Number.isFinite(value));
      const ctx = canvas.getContext('2d');
      canvas.width = canvas.clientWidth * devicePixelRatio;
      canvas.height = canvas.clientHeight * devicePixelRatio;
      ctx.scale(devicePixelRatio, devicePixelRatio);
      const width = canvas.clientWidth;
      const height = canvas.clientHeight;
      ctx.clearRect(0, 0, width, height);
      const padding = { left: 46, right: 16, top: 14, bottom: 26 };
      if (!values.length) {
        ctx.fillText('No data', padding.left, 28);
        return;
      }
      let min = Math.min(...values);
      let max = Math.max(...values);
      if (min === max) { min -= 1; max += 1; }
      const minX = 0;
      const maxX = maxXAxisValue(xA, xB, seriesA.length, seriesB.length);
      const chartWidth = width - padding.left - padding.right;
      const chartHeight = height - padding.top - padding.bottom;
      const xFor = value => padding.left + chartWidth * ((value - minX) / Math.max(1, maxX - minX));
      const yFor = value => padding.top + chartHeight * (1 - (value - min) / (max - min));
      ctx.strokeStyle = 'rgba(128,128,128,.45)';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(padding.left, padding.top);
      ctx.lineTo(padding.left, height - padding.bottom);
      ctx.lineTo(width - padding.right, height - padding.bottom);
      ctx.stroke();
      ctx.fillStyle = getComputedStyle(document.body).color;
      ctx.font = '12px Segoe UI, Arial';
      ctx.fillText(fmt(max), 4, padding.top + 4);
      ctx.fillText(fmt(min), 4, height - padding.bottom);
      const step = channel.key === 'gear' || channel.kind === 'step';
      drawSeries(ctx, xA, seriesA, xFor, yFor, '#2563eb', step);
      drawSeries(ctx, xB, seriesB, xFor, yFor, '#d97706', step);
      canvas._chartData = { channel, lapA, lapB, xA, xB, seriesA, seriesB };
      legend.textContent = `blue=${lapA ? lapLabel(lapA) : '-'}  orange=${lapB ? lapLabel(lapB) : '-'}`;
    }

    function drawSeries(ctx, xValues, values, xFor, yFor, color, step) {
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.beginPath();
      let started = false;
      let lastY = null;
      values.forEach((value, index) => {
        if (typeof value !== 'number' || !Number.isFinite(value)) return;
        const x = xFor(xValues[index]);
        const y = yFor(value);
        if (!started) {
          ctx.moveTo(x, y);
          started = true;
          lastY = y;
          return;
        }
        if (step) {
          ctx.lineTo(x, lastY);
          ctx.lineTo(x, y);
        } else {
          ctx.lineTo(x, y);
        }
        lastY = y;
      });
      if (started) ctx.stroke();
    }

    function valuesFor(lap, key) {
      if (key === 'gear') return gearDisplayValues(lap);
      return ((lap.series || {})[key] || []).map(value => Number.isFinite(Number(value)) ? Number(value) : null);
    }

    function gearDisplayValues(lap) {
      const raw = ((lap.series || {}).gear || []).map(value => Number.isFinite(Number(value)) ? Number(value) : null);
      const speed = ((lap.series || {}).speed_kph || []).map(value => Number.isFinite(Number(value)) ? Number(value) : null);
      return raw.map((value, index) => {
        if (value !== 0 || !(speed[index] > 5)) return value;
        const previous = nearestNonZeroGear(raw, index, -1);
        const next = nearestNonZeroGear(raw, index, 1);
        if (previous !== null && next !== null) return previous;
        return value;
      });
    }

    function nearestNonZeroGear(values, startIndex, direction) {
      for (let index = startIndex + direction; index >= 0 && index < values.length && Math.abs(index - startIndex) <= 3; index += direction) {
        const value = values[index];
        if (Number.isFinite(value) && value !== 0) return value;
      }
      return null;
    }

    function xValues(lap, length) {
      if (xMode.value === 'sample') return Array.from({ length }, (_, index) => index);
      if (xMode.value === 'time') {
        const times = valuesFor(lap, 'time_seconds');
        if (times.length === length && times.some(value => Number.isFinite(value))) return times.map((value, index) => Number.isFinite(value) ? value : index);
        return Array.from({ length }, (_, index) => index);
      }
      const percents = valuesFor(lap, 'lap_percent');
      if (percents.length === length) return percents.map((value, index) => Number.isFinite(value) ? value : index / Math.max(1, length - 1) * 100);
      return Array.from({ length }, (_, index) => index / Math.max(1, length - 1) * 100);
    }

    function maxXAxisValue(xA, xB, lengthA, lengthB) {
      if (xMode.value === 'sample') return Math.max(lengthA, lengthB, 1) - 1;
      if (xMode.value === 'time') {
        const values = [...xA, ...xB].filter(value => Number.isFinite(value));
        return values.length ? Math.max(...values, 1) : Math.max(lengthA, lengthB, 1) - 1;
      }
      return 100;
    }

    function nearestIndex(values, target) {
      let best = 0;
      let distance = Number.POSITIVE_INFINITY;
      values.forEach((value, index) => {
        const nextDistance = Math.abs(value - target);
        if (nextDistance < distance) { best = index; distance = nextDistance; }
      });
      return best;
    }

    function showTooltip(event) {
      const data = event.currentTarget._chartData;
      if (!data) return;
      const rect = event.currentTarget.getBoundingClientRect();
      const percent = Math.max(0, Math.min(1, (event.clientX - rect.left - 46) / Math.max(1, rect.width - 62)));
      const maxX = xMode.value === 'sample'
        ? Math.max(data.seriesA.length, data.seriesB.length, 1) - 1
        : xMode.value === 'time'
        ? maxXAxisValue(data.xA || [], data.xB || [], data.seriesA.length, data.seriesB.length)
        : 100;
      const target = percent * maxX;
      const indexA = nearestIndex(data.xA || [], target);
      const indexB = nearestIndex(data.xB || [], target);
      tooltipEl.textContent = [
        data.channel.label,
        `A ${data.lapA ? data.lapA.driver_name : '-'}: ${fmt((data.seriesA || [])[indexA])}`,
        `B ${data.lapB ? data.lapB.driver_name : '-'}: ${fmt((data.seriesB || [])[indexB])}`,
      ].join('\\n');
      tooltipEl.style.display = 'block';
      tooltipEl.style.left = `${event.clientX + 12}px`;
      tooltipEl.style.top = `${event.clientY + 12}px`;
    }

    function hideTooltip() {
      tooltipEl.style.display = 'none';
    }

    function reportFromJsonl(text) {
      const samples = text.split(/\\r?\\n/).map(line => line.trim()).filter(Boolean).map(line => JSON.parse(line));
      const groups = new Map();
      for (const sample of samples) {
        const key = `${sample.driver_id}:${sample.lap_number}`;
        if (!groups.has(key)) groups.set(key, []);
        groups.get(key).push(sample);
      }
      const laps = [];
      for (const [key, group] of groups) {
        const first = group[0] || {};
        const percents = group.map(sample => Number(sample.lap_percent)).filter(Number.isFinite);
        const minPercent = percents.length ? Math.min(...percents) : null;
        const maxPercent = percents.length ? Math.max(...percents) : null;
        const lapId = `${first.driver_id}:${first.lap_number}`;
        laps.push({
          lap_id: lapId,
          driver_id: first.driver_id,
          driver_name: first.driver_name,
          vehicle_name: first.vehicle_name,
          lap_number: first.lap_number,
          lap_time: null,
          sample_count: group.length,
          coverage: { min_percent: minPercent, max_percent: maxPercent },
          lap_classification: 'uploaded',
          classification_reasons: [],
          eligible_for_report: false,
          series: seriesFromSamples(group),
        });
      }
      return {
        session_id: 'uploaded-jsonl',
        track: (samples[0] || {}).session_track,
        session_type: (samples[0] || {}).session_type,
        status: 'uploaded',
        channels: defaultChannels(),
        laps: [],
        all_laps: laps,
        proper_lap_count: 0,
        excluded_lap_count: laps.length,
        telemetry_sample_count: samples.length,
        reference_lap: null,
      };
    }

    function seriesFromSamples(samples) {
      const fields = ['lap_percent','time_seconds','timestamp','session_time','speed_kph','throttle_percent','brake_percent','steering_percent','gear','lateral_g','longitudinal_g','vertical_g','g_magnitude'];
      const series = {};
      for (const field of fields) series[field] = samples.map(sample => Number.isFinite(Number(sample[field])) ? Number(sample[field]) : null);
      return series;
    }

    function defaultChannels() {
      return [
        { key: 'speed_kph', label: 'Speed', unit: 'km/h', kind: 'line' },
        { key: 'throttle_percent', label: 'Throttle', unit: '%', kind: 'line' },
        { key: 'brake_percent', label: 'Brake', unit: '%', kind: 'line' },
        { key: 'steering_percent', label: 'Steering', unit: '%', kind: 'line' },
        { key: 'gear', label: 'Gear', unit: '', kind: 'step' },
        { key: 'lateral_g', label: 'Lateral G', unit: 'g', kind: 'line' },
        { key: 'longitudinal_g', label: 'Longitudinal G', unit: 'g', kind: 'line' },
        { key: 'vertical_g', label: 'Vertical G', unit: 'g', kind: 'line' },
      ];
    }

    function reportFromText(text, fileName) {
      if ((fileName || '').toLowerCase().endsWith('.jsonl')) return reportFromJsonl(text);
      try {
        return JSON.parse(text);
      } catch (error) {
        return reportFromJsonl(text);
      }
    }

    loadSelected.onclick = () => loadReport(recordingSelect.value).catch(error => { statusEl.textContent = `error=${error}`; });
    fileInput.onchange = () => {
      const file = fileInput.files && fileInput.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = () => {
        try {
          const text = String(reader.result || '');
          const payload = reportFromText(text, file.name);
          renderReport(payload);
        } catch (error) {
          statusEl.textContent = `error=${error}`;
        }
      };
      reader.readAsText(file);
    };

    loadRecordings().then(() => {
      if (initialSessionId) {
        recordingSelect.value = initialSessionId;
        loadReport(initialSessionId).catch(error => { statusEl.textContent = `error=${error}`; });
      } else {
        statusEl.textContent = 'Select a stored session or open a file.';
      }
    });
  </script>
</body>
</html>"""
    return html.replace("__SESSION_ID__", safe_session_id)
