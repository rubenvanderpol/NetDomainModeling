/** Narrow `document.querySelector` / event targets without repeated casts. */
export function queryHtmlElement<T extends HTMLElement>(selector: string): T | null {
  const el = document.querySelector(selector);
  return el instanceof HTMLElement ? (el as T) : null;
}

export function eventTargetElement(ev: Event): Element | null {
  const t = ev.target;
  return t instanceof Element ? t : null;
}

export function closestFromEvent(ev: Event, selector: string): HTMLElement | null {
  const el = eventTargetElement(ev)?.closest(selector);
  return el instanceof HTMLElement ? el : null;
}

export function tagNameFromEvent(ev: Event): string | undefined {
  return eventTargetElement(ev)?.tagName;
}

export function asHtmlInput(el: Element | null): HTMLInputElement | null {
  return el instanceof HTMLInputElement ? el : null;
}

export function asHtmlTextArea(el: Element | null): HTMLTextAreaElement | null {
  return el instanceof HTMLTextAreaElement ? el : null;
}

export function asHtmlSelect(el: Element | null): HTMLSelectElement | null {
  return el instanceof HTMLSelectElement ? el : null;
}
