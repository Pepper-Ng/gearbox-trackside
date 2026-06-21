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
    <label>X axis <select id="xMode"><option value="track">Track %</option><option value="sample">Sample</option></select></label>
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

    function lapLabel(lap) {
      const tags = [];
      if (lap.is_fastest_overall) tags.push('Fastest Ovrl.');
      else if (lap.is_fastest_personal) tags.push('Fastest Pers.');
      const tagText = tags.length ? ` (${tags.join(', ')})` : '';
      const kind = lap.lap_classification || (lap.eligible_for_report ? 'proper' : 'partial');
      return `${lap.driver_name || lap.driver_id} lap ${fmt(lap.lap_number)} ${fmtTime(lap.lap_time)} ${kind}${tagText}`;
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
      report = payload;
      const laps = allLaps();
      const proper = laps.filter(lap => lap.eligible_for_report);
      const reference = laps.find(lap => lap.is_fastest_overall) || proper[0] || laps[0];
      defaultA = reference ? reference.lap_id : '';
      defaultB = (proper.find(lap => lap.lap_id !== defaultA) || laps.find(lap => lap.lap_id !== defaultA) || reference || {}).lap_id || '';
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
      const selectA = lapSelect(defaultA);
      const selectB = lapSelect(defaultB);
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

    function lapSelect(selectedValue) {
      const select = document.createElement('select');
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
      const minX = xMode.value === 'sample' ? 0 : 0;
      const maxX = xMode.value === 'sample' ? Math.max(seriesA.length, seriesB.length, 1) - 1 : 100;
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
      drawSeries(ctx, xA, seriesA, xFor, yFor, '#2563eb');
      drawSeries(ctx, xB, seriesB, xFor, yFor, '#d97706');
      canvas._chartData = { channel, lapA, lapB, xA, xB, seriesA, seriesB };
      legend.textContent = `blue=${lapA ? lapLabel(lapA) : '-'}  orange=${lapB ? lapLabel(lapB) : '-'}`;
    }

    function drawSeries(ctx, xValues, values, xFor, yFor, color) {
      ctx.strokeStyle = color;
      ctx.lineWidth = 2;
      ctx.beginPath();
      let started = false;
      values.forEach((value, index) => {
        if (typeof value !== 'number' || !Number.isFinite(value)) return;
        const x = xFor(xValues[index]);
        const y = yFor(value);
        if (!started) { ctx.moveTo(x, y); started = true; } else { ctx.lineTo(x, y); }
      });
      if (started) ctx.stroke();
    }

    function valuesFor(lap, key) {
      return ((lap.series || {})[key] || []).map(value => Number.isFinite(Number(value)) ? Number(value) : null);
    }

    function xValues(lap, length) {
      if (xMode.value === 'sample') return Array.from({ length }, (_, index) => index);
      const percents = valuesFor(lap, 'lap_percent');
      if (percents.length === length) return percents.map((value, index) => Number.isFinite(value) ? value : index / Math.max(1, length - 1) * 100);
      return Array.from({ length }, (_, index) => index / Math.max(1, length - 1) * 100);
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
        : 100;
      const target = percent * maxX;
      const indexA = nearestIndex(data.xA || [], target);
      const indexB = nearestIndex(data.xB || [], target);
      tooltipEl.textContent = [
        data.channel.label,
        `A ${data.lapA ? data.lapA.driver_name : '-'}: ${fmt((data.seriesA || [])[indexA])}`,
        `B ${data.lapB ? data.lapB.driver_name : '-'}: ${fmt((data.seriesB || [])[indexB])}`,
      ].join('\n');
      tooltipEl.style.display = 'block';
      tooltipEl.style.left = `${event.clientX + 12}px`;
      tooltipEl.style.top = `${event.clientY + 12}px`;
    }

    function hideTooltip() {
      tooltipEl.style.display = 'none';
    }

    function reportFromJsonl(text) {
      const samples = text.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(line => JSON.parse(line));
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
        { key: 'speed_kph', label: 'Speed', unit: 'km/h' },
        { key: 'throttle_percent', label: 'Throttle', unit: '%' },
        { key: 'brake_percent', label: 'Brake', unit: '%' },
        { key: 'steering_percent', label: 'Steering', unit: '%' },
        { key: 'gear', label: 'Gear', unit: '' },
        { key: 'lateral_g', label: 'Lateral G', unit: 'g' },
        { key: 'longitudinal_g', label: 'Longitudinal G', unit: 'g' },
        { key: 'vertical_g', label: 'Vertical G', unit: 'g' },
      ];
    }

    loadSelected.onclick = () => loadReport(recordingSelect.value).catch(error => { statusEl.textContent = `error=${error}`; });
    fileInput.onchange = () => {
      const file = fileInput.files && fileInput.files[0];
      if (!file) return;
      const reader = new FileReader();
      reader.onload = () => {
        try {
          const text = String(reader.result || '');
          const payload = text.trim().startsWith('{') ? JSON.parse(text) : reportFromJsonl(text);
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
