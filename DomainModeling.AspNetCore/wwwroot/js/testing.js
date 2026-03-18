/**
 * Aggregate Testing — create instances, fill parameters, and store to repository.
 */
import { esc, escAttr, shortName } from './helpers.js';
import { renderTabBar } from './tabs.js';

// ── State ────────────────────────────────────────────
let apiUrl = '';
let aggregates = [];
let selectedAggregate = null;
let creationMethod = 'properties'; // 'properties' | 'constructor:{idx}' | 'factory:{name}'
let instances = [];
let creating = false;
let error = null;
let expandedInstances = new Set();
let editingInstance = null; // id of the instance being edited
let invokeMethod = null; // { id, methodName } of the method being invoked
let invokeResult = null; // last method invocation result

// ── Init ─────────────────────────────────────────────

/** Load aggregate metadata and stored instances from the API. */
export async function initTesting(url) {
  apiUrl = url;
  try {
    const [aggRes, instRes] = await Promise.all([
      fetch(`${apiUrl}/testing/aggregates`),
      fetch(`${apiUrl}/testing/instances`),
    ]);
    aggregates = await aggRes.json();
    instances = await instRes.json();
    if (aggregates.length > 0) {
      selectedAggregate = aggregates[0];
      autoSelectMethod();
    }
  } catch (e) {
    console.error('Failed to load testing data:', e);
  }
}

// ── Render ───────────────────────────────────────────

/** Returns the full HTML for the testing view. */
export function renderTestingView() {
  let html = renderTabBar('testing');

  html += '<div class="testing-body">';

  // Left: create panel
  html += '<div class="testing-create-panel">';
  html += renderCreateForm();
  html += '</div>';

  // Right: instances panel
  html += '<div class="testing-instances-panel">';
  html += renderInstanceList();
  html += '</div>';

  html += '</div>';
  return html;
}

/** Called after the HTML is in the DOM. */
export function mountTesting() {
  // No special mounting needed — event handlers are inline onclick
}

// ── Create form ──────────────────────────────────────

function renderCreateForm() {
  let html = '<div class="testing-section-title">Create Aggregate</div>';

  if (aggregates.length === 0) {
    html += '<div class="testing-empty">No aggregate types found in the domain graph.</div>';
    return html;
  }

  // Type selector
  html += '<div class="testing-field">';
  html += '<label>Aggregate Type</label>';
  html += '<select class="testing-select" onchange="window.__testing.selectType(this.value)">';
  for (const agg of aggregates) {
    const sel = agg.fullName === selectedAggregate?.fullName ? ' selected' : '';
    html += `<option value="${escAttr(agg.fullName)}"${sel}>${esc(agg.name)}</option>`;
  }
  html += '</select>';
  html += '</div>';

  if (!selectedAggregate) return html;

  // Description
  if (selectedAggregate.description) {
    html += `<div class="testing-desc">${esc(selectedAggregate.description)}</div>`;
  }

  // Creation method selector
  html += '<div class="testing-field">';
  html += '<label>Creation Method</label>';
  html += '<select class="testing-select" onchange="window.__testing.selectMethod(this.value)">';

  // Properties (JSON deserialization)
  html += `<option value="properties"${creationMethod === 'properties' ? ' selected' : ''}>Properties (JSON deserialization)</option>`;

  // Constructors with parameters
  (selectedAggregate.constructors || []).forEach((c, i) => {
    if (c.parameters.length > 0) {
      const sig = c.parameters.map(p => p.typeName).join(', ');
      const val = `constructor:${i}`;
      html += `<option value="${val}"${creationMethod === val ? ' selected' : ''}>Constructor(${esc(sig)})</option>`;
    }
  });

  // Factory methods
  for (const f of (selectedAggregate.factoryMethods || [])) {
    const sig = f.parameters.map(p => p.typeName).join(', ');
    const val = `factory:${f.name}`;
    const star = f.name === selectedAggregate.configuredFactory ? ' ★' : '';
    html += `<option value="${val}"${creationMethod === val ? ' selected' : ''}>${esc(f.name)}(${esc(sig)})${star}</option>`;
  }

  html += '</select>';
  html += '</div>';

  // Parameter / property fields
  html += '<div class="testing-params" id="testingParams">';
  html += renderParameterFields();
  html += '</div>';

  // Error
  if (error) {
    html += `<div class="testing-error">${esc(error)}</div>`;
  }

  // Create button
  html += `<button class="testing-create-btn" onclick="window.__testing.create()" ${creating ? 'disabled' : ''}>`;
  html += creating ? 'Creating…' : 'Create & Store';
  html += '</button>';

  return html;
}

function renderParameterFields() {
  let params = [];

  if (creationMethod === 'properties') {
    params = selectedAggregate.properties || [];
  } else if (creationMethod.startsWith('constructor:')) {
    const idx = parseInt(creationMethod.split(':')[1]);
    params = selectedAggregate.constructors[idx]?.parameters || [];
  } else if (creationMethod.startsWith('factory:')) {
    const name = creationMethod.split(':').slice(1).join(':');
    const factory = (selectedAggregate.factoryMethods || []).find(f => f.name === name);
    params = factory?.parameters || [];
  }

  if (params.length === 0) {
    return '<div class="testing-hint">No parameters required. Click Create to instantiate with default values.</div>';
  }

  let html = '';
  for (const p of params) {
    html += renderSingleField(p, '');
  }
  return html;
}

/** Render a single parameter/property field, with nested fields for complex types. */
function renderSingleField(p, prefix) {
  const fullName = prefix ? `${prefix}.${p.name}` : p.name;
  const req = p.isRequired ? ' <span class="testing-required">*</span>' : '';
  const complex = p.isComplex || false;
  const hasSubProps = complex && Array.isArray(p.subProperties) && p.subProperties.length > 0;

  let html = '';

  if (hasSubProps) {
    // Render as a collapsible object group with nested sub-fields
    html += '<div class="testing-field testing-object-group">';
    html += `<div class="testing-object-header">`;
    html += `<span class="testing-object-label">${esc(p.name)}${req}</span>`;
    html += `<span class="testing-type-hint">${esc(p.typeName)}</span>`;
    html += '</div>';
    html += '<div class="testing-object-fields">';
    for (const sub of p.subProperties) {
      html += renderSingleField(sub, fullName);
    }
    html += '</div>';
    html += '</div>';
  } else if (p.isCollection) {
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" placeholder='[ ]' spellcheck="false"></textarea>`;
    html += '</div>';
  } else if (complex) {
    // Complex but no sub-properties known — fall back to JSON textarea
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" placeholder='{ }' spellcheck="false"></textarea>`;
    html += '</div>';
  } else if (p.typeName === 'bool') {
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<select class="testing-select testing-input" data-param="${escAttr(fullName)}">`;
    html += '<option value="">— select —</option>';
    html += '<option value="true">true</option>';
    html += '<option value="false">false</option>';
    html += '</select>';
    html += '</div>';
  } else {
    const ph = p.defaultValue || placeholder(p.typeName);
    const inputType = inputTypeFor(p.typeName);
    const step = ['decimal', 'double', 'float'].includes(p.typeName) ? ' step="any"' : '';
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<input class="testing-input" type="${inputType}" data-param="${escAttr(fullName)}" placeholder="${escAttr(ph)}"${step} />`;
    html += '</div>';
  }

  return html;
}

function inputTypeFor(typeName) {
  if (['int', 'long', 'decimal', 'double', 'float'].includes(typeName)) return 'number';
  return 'text';
}

function placeholder(typeName) {
  const map = {
    'string': 'Enter text…',
    'int': '0', 'long': '0',
    'decimal': '0.00', 'double': '0.0', 'float': '0.0',
    'bool': 'true / false',
    'Guid': '00000000-0000-0000-0000-000000000000',
    'DateTime': '2026-01-01T00:00:00',
  };
  return map[typeName] || '';
}

function collectParameters() {
  const inputs = document.querySelectorAll('#testingParams .testing-input');
  const params = {};

  for (const input of inputs) {
    const path = input.dataset.param;
    let value = (input.value || '').trim();
    if (!value) continue;

    let parsed;
    if (input.dataset.complex === 'true') {
      try { parsed = JSON.parse(value); }
      catch (e) { throw new Error(`Invalid JSON for "${path}": ${e.message}`); }
    } else if (input.type === 'number') {
      parsed = value.includes('.') ? parseFloat(value) : parseInt(value);
    } else if (value === 'true' || value === 'false') {
      parsed = value === 'true';
    } else {
      parsed = value;
    }

    setNested(params, path, parsed);
  }
  return params;
}

/** Set a value at a dotted path (e.g. "Price.Amount") in an object. */
function setNested(obj, path, value) {
  const parts = path.split('.');
  let cur = obj;
  for (let i = 0; i < parts.length - 1; i++) {
    if (!(parts[i] in cur)) cur[parts[i]] = {};
    cur = cur[parts[i]];
  }
  cur[parts[parts.length - 1]] = value;
}

// ── Instance list ────────────────────────────────────

function renderInstanceList() {
  let html = `<div class="testing-section-title">Stored Instances <span class="testing-count">${instances.length}</span></div>`;

  if (instances.length === 0) {
    html += '<div class="testing-empty">No instances created yet.<br>Use the form on the left to create and store aggregates.</div>';
    return html;
  }

  for (const inst of instances) {
    const expanded = expandedInstances.has(inst.id);
    const isEditing = editingInstance === inst.id;
    html += '<div class="testing-instance">';

    html += '<div class="testing-instance-header">';
    html += `<span class="testing-instance-type">${esc(inst.typeName)}</span>`;
    html += `<span class="testing-instance-id" title="${escAttr(inst.id)}">${esc(inst.id.substring(0, 8))}…</span>`;
    html += `<button class="testing-expand-btn" onclick="window.__testing.toggleInstance('${escAttr(inst.id)}')" title="${expanded ? 'Collapse' : 'Expand'}">${expanded ? '▾' : '▸'}</button>`;
    html += `<button class="testing-edit-btn${isEditing ? ' active' : ''}" onclick="window.__testing.editInstance('${escAttr(inst.id)}')" title="${isEditing ? 'Close editor' : 'Edit & invoke methods'}">✎</button>`;
    html += `<button class="testing-delete-btn" onclick="window.__testing.deleteInstance('${escAttr(inst.id)}')" title="Delete instance">✕</button>`;
    html += '</div>';

    if (isEditing) {
      html += renderInstanceEditor(inst);
    } else if (expanded) {
      html += '<div class="testing-instance-props">';
      html += '<pre>' + highlight(JSON.stringify(inst.properties, null, 2)) + '</pre>';
      html += '</div>';
    }

    html += '</div>';
  }

  return html;
}

// ── Instance editor (update properties + invoke methods) ─────────

function renderInstanceEditor(inst) {
  const agg = aggregates.find(a => a.fullName === inst.typeFullName);
  const methods = agg?.methods || [];

  let html = '<div class="testing-instance-editor">';

  // ── Properties tab ──
  html += '<div class="testing-editor-section">';
  html += '<div class="testing-editor-section-title">Properties</div>';
  html += `<div class="testing-editor-props" id="editProps-${escAttr(inst.id)}">`;

  const props = agg?.properties || [];
  if (props.length > 0) {
    for (const p of props) {
      const currentVal = getNestedValue(inst.properties, p.name);
      html += renderEditField(p, '', inst.id, currentVal);
    }
  } else {
    // Fallback: raw JSON edit
    html += `<textarea class="testing-input testing-textarea testing-edit-area" id="editJson-${escAttr(inst.id)}" spellcheck="false">${esc(JSON.stringify(inst.properties, null, 2))}</textarea>`;
  }

  html += '</div>';
  html += '<div class="testing-edit-actions">';
  html += `<button class="testing-save-btn" onclick="window.__testing.saveInstance('${escAttr(inst.id)}')">Update</button>`;
  html += `<button class="testing-cancel-btn" onclick="window.__testing.cancelEdit('${escAttr(inst.id)}')">Cancel</button>`;
  html += '</div>';
  html += '</div>';

  // ── Methods tab ──
  if (methods.length > 0) {
    html += '<div class="testing-editor-section">';
    html += '<div class="testing-editor-section-title">Methods</div>';
    html += '<div class="testing-methods-list">';
    for (const m of methods) {
      const isInvoking = invokeMethod?.id === inst.id && invokeMethod?.methodName === m.name;
      const sig = m.parameters.map(p => `${p.typeName} ${p.name}`).join(', ');

      html += '<div class="testing-method-card">';
      html += '<div class="testing-method-header">';
      html += `<span class="testing-method-name">${esc(m.name)}</span>`;
      html += `<span class="testing-method-sig">(${esc(sig)})</span>`;
      html += `<span class="testing-method-return">${esc(m.returnTypeName)}</span>`;
      html += '</div>';

      if (isInvoking) {
        html += `<div class="testing-method-params" id="methodParams-${escAttr(inst.id)}-${escAttr(m.name)}">`;
        if (m.parameters.length > 0) {
          for (const p of m.parameters) {
            html += renderSingleField(p, '', `methodParam-${inst.id}-${m.name}`);
          }
        }
        html += '<div class="testing-method-actions">';
        html += `<button class="testing-invoke-btn" onclick="window.__testing.invokeMethod('${escAttr(inst.id)}', '${escAttr(m.name)}')">Invoke</button>`;
        html += `<button class="testing-cancel-btn" onclick="window.__testing.cancelInvoke()">Cancel</button>`;
        html += '</div>';
        html += '</div>';

        // Show last invocation result
        if (invokeResult && invokeResult.methodName === m.name && invokeResult.instanceId === inst.id) {
          html += renderInvokeResult(invokeResult);
        }
      } else {
        html += `<button class="testing-expand-method-btn" onclick="window.__testing.startInvoke('${escAttr(inst.id)}', '${escAttr(m.name)}')">▶ Invoke</button>`;
      }

      html += '</div>';
    }
    html += '</div>';
    html += '</div>';
  }

  html += '</div>';
  return html;
}

/** Render a single edit field pre-filled with current value. */
function renderEditField(p, prefix, instanceId, currentValue) {
  const fullName = prefix ? `${prefix}.${p.name}` : p.name;
  const req = p.isRequired ? ' <span class="testing-required">*</span>' : '';
  const complex = p.isComplex || false;
  const hasSubProps = complex && Array.isArray(p.subProperties) && p.subProperties.length > 0;

  let html = '';

  if (hasSubProps) {
    html += '<div class="testing-field testing-object-group">';
    html += '<div class="testing-object-header">';
    html += `<span class="testing-object-label">${esc(p.name)}${req}</span>`;
    html += `<span class="testing-type-hint">${esc(p.typeName)}</span>`;
    html += '</div>';
    html += '<div class="testing-object-fields">';
    for (const sub of p.subProperties) {
      const subVal = currentValue != null ? getNestedValue(currentValue, sub.name) : undefined;
      html += renderEditField(sub, fullName, instanceId, subVal);
    }
    html += '</div>';
    html += '</div>';
  } else if (p.isCollection) {
    const val = currentValue != null ? JSON.stringify(currentValue, null, 2) : '';
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" spellcheck="false">${esc(val)}</textarea>`;
    html += '</div>';
  } else if (complex) {
    const val = currentValue != null ? JSON.stringify(currentValue, null, 2) : '';
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<textarea class="testing-input testing-textarea" data-param="${escAttr(fullName)}" data-complex="true" spellcheck="false">${esc(val)}</textarea>`;
    html += '</div>';
  } else if (p.typeName === 'bool') {
    const val = currentValue != null ? String(currentValue) : '';
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<select class="testing-select testing-input" data-param="${escAttr(fullName)}">`;
    html += `<option value="">— select —</option>`;
    html += `<option value="true"${val === 'true' ? ' selected' : ''}>true</option>`;
    html += `<option value="false"${val === 'false' ? ' selected' : ''}>false</option>`;
    html += '</select>';
    html += '</div>';
  } else {
    const val = currentValue != null ? String(currentValue) : '';
    const inputType = inputTypeFor(p.typeName);
    const step = ['decimal', 'double', 'float'].includes(p.typeName) ? ' step="any"' : '';
    html += '<div class="testing-field">';
    html += `<label>${esc(p.name)}${req} <span class="testing-type-hint">${esc(p.typeName)}</span></label>`;
    html += `<input class="testing-input" type="${inputType}" data-param="${escAttr(fullName)}" value="${escAttr(val)}"${step} />`;
    html += '</div>';
  }

  return html;
}

/** Render the method invocation result (events + updated properties). */
function renderInvokeResult(result) {
  let html = '<div class="testing-invoke-result">';
  if (result.error) {
    html += `<div class="testing-error">${esc(result.error)}</div>`;
  } else {
    if (result.raisedEvents && result.raisedEvents.length > 0) {
      html += '<div class="testing-events-raised">';
      html += '<div class="testing-events-title">⚡ Events raised:</div>';
      for (const evt of result.raisedEvents) {
        html += '<div class="testing-event-card">';
        html += `<span class="testing-event-name">${esc(evt.typeName)}</span>`;
        html += '<pre>' + highlight(JSON.stringify(evt.properties, null, 2)) + '</pre>';
        html += '</div>';
      }
      html += '</div>';
    }
    html += '<div class="testing-invoke-success">✓ Method invoked — instance updated.</div>';
  }
  html += '</div>';
  return html;
}

/** Get a nested value from an object by property name (case-insensitive first char). */
function getNestedValue(obj, name) {
  if (obj == null || typeof obj !== 'object') return undefined;
  if (name in obj) return obj[name];
  const camel = name[0].toLowerCase() + name.slice(1);
  if (camel in obj) return obj[camel];
  const pascal = name[0].toUpperCase() + name.slice(1);
  if (pascal in obj) return obj[pascal];
  return undefined;
}

/** Collect parameters from a scoped container. */
function collectScopedParams(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return {};
  const inputs = container.querySelectorAll('.testing-input');
  const params = {};
  for (const input of inputs) {
    const path = input.dataset.param;
    if (!path) continue;
    let value = (input.value || '').trim();
    if (!value) continue;

    let parsed;
    if (input.dataset.complex === 'true') {
      try { parsed = JSON.parse(value); }
      catch (e) { throw new Error(`Invalid JSON for "${path}": ${e.message}`); }
    } else if (input.type === 'number') {
      parsed = value.includes('.') ? parseFloat(value) : parseInt(value);
    } else if (value === 'true' || value === 'false') {
      parsed = value === 'true';
    } else {
      parsed = value;
    }

    setNested(params, path, parsed);
  }
  return params;
}

function highlight(json) {
  return json
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"([^"]+)":/g, '<span class="json-key">"$1"</span>:')
    .replace(/: "([^"]*)"/g, ': <span class="json-string">"$1"</span>')
    .replace(/: (\d+\.?\d*)/g, ': <span class="json-number">$1</span>')
    .replace(/: (true|false)/g, ': <span class="json-bool">$1</span>')
    .replace(/: (null)/g, ': <span class="json-null">$1</span>');
}

// ── Helpers ──────────────────────────────────────────

function autoSelectMethod() {
  if (selectedAggregate?.configuredFactory) {
    creationMethod = `factory:${selectedAggregate.configuredFactory}`;
  } else {
    creationMethod = 'properties';
  }
}

function refresh() {
  const main = document.getElementById('mainContent');
  if (main) main.innerHTML = renderTestingView();
}

// ── Public API (exposed as window.__testing) ─────────

export function selectType(fullName) {
  selectedAggregate = aggregates.find(a => a.fullName === fullName) || null;
  error = null;
  autoSelectMethod();
  refresh();
}

export function selectMethod(method) {
  creationMethod = method;
  error = null;
  refresh();
}

export async function create() {
  if (!selectedAggregate || creating) return;

  error = null;
  creating = true;
  refresh();

  try {
    const params = collectParameters();

    let factoryMethod = null;
    if (creationMethod.startsWith('factory:')) {
      factoryMethod = creationMethod.split(':').slice(1).join(':');
    }

    const body = {
      typeFullName: selectedAggregate.fullName,
      factoryMethod,
      parameters: Object.keys(params).length > 0 ? params : null,
    };

    const res = await fetch(`${apiUrl}/testing/create`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }

    const instance = await res.json();
    instances.unshift(instance);
    expandedInstances.add(instance.id); // auto-expand newly created
  } catch (e) {
    error = e.message;
  } finally {
    creating = false;
    refresh();
  }
}

export async function deleteInstance(id) {
  try {
    await fetch(`${apiUrl}/testing/instances/${id}`, { method: 'DELETE' });
    instances = instances.filter(i => i.id !== id);
    expandedInstances.delete(id);
    editingInstances.delete(id);
    refresh();
  } catch (e) {
    error = e.message;
    refresh();
  }
}

export function editInstance(id) {
  if (editingInstance === id) {
    // Toggle off
    editingInstance = null;
    invokeMethod = null;
    invokeResult = null;
    refresh();
    return;
  }
  editingInstance = id;
  invokeMethod = null;
  invokeResult = null;
  expandedInstances.delete(id);
  refresh();
}

export function cancelEdit() {
  editingInstance = null;
  invokeMethod = null;
  invokeResult = null;
  refresh();
}

export async function saveInstance(id) {
  try {
    const agg = aggregates.find(a => {
      const inst = instances.find(i => i.id === id);
      return inst && a.fullName === inst.typeFullName;
    });

    let params;
    if (agg?.properties?.length > 0) {
      params = collectScopedParams(`editProps-${id}`);
    } else {
      const textarea = document.getElementById(`editJson-${id}`);
      if (!textarea) return;
      params = JSON.parse(textarea.value);
    }

    const res = await fetch(`${apiUrl}/testing/instances/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ parameters: params }),
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }

    const updated = await res.json();
    instances = instances.map(i => i.id === id ? updated : i);
    error = null;
  } catch (e) {
    error = e.message;
  }
  refresh();
}

export function toggleInstance(id) {
  if (expandedInstances.has(id)) expandedInstances.delete(id);
  else expandedInstances.add(id);
  refresh();
}

export function startInvoke(instanceId, methodName) {
  invokeMethod = { id: instanceId, methodName };
  invokeResult = null;
  refresh();
}

export function cancelInvoke() {
  invokeMethod = null;
  invokeResult = null;
  refresh();
}

export async function invokeInstanceMethod(instanceId, methodName) {
  try {
    const agg = aggregates.find(a => {
      const inst = instances.find(i => i.id === instanceId);
      return inst && a.fullName === inst.typeFullName;
    });
    const method = agg?.methods?.find(m => m.name === methodName);

    let params = {};
    if (method?.parameters?.length > 0) {
      params = collectScopedParams(`methodParams-${instanceId}-${methodName}`);
    }

    const res = await fetch(`${apiUrl}/testing/instances/${instanceId}/invoke`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ methodName, parameters: params }),
    });

    if (!res.ok) {
      const errData = await res.json().catch(() => null);
      throw new Error(errData?.error || `HTTP ${res.status}`);
    }

    const result = await res.json();

    // Update the local instance with the new state
    if (result.instance) {
      instances = instances.map(i => i.id === instanceId ? result.instance : i);
    }

    invokeResult = {
      instanceId,
      methodName,
      raisedEvents: result.raisedEvents || [],
    };
    error = null;
  } catch (e) {
    invokeResult = {
      instanceId,
      methodName,
      error: e.message,
    };
  }
  refresh();
}
