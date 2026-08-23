(function (window, document) {
  'use strict';

  function escapeHtml(value) {
    return String(value ?? '').replace(/[&<>"']/g, character => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#39;'
    }[character]));
  }

  function escapeAttribute(value) {
    return escapeHtml(value).replace(/`/g, '');
  }

  /**
   * @param {HTMLFormElement} form
   * @param {{ mode?: 'sku'|'variantId', portalRoot?: HTMLElement }} options
   *   sku (Thêm đơn): list stays in-form, position absolute — do not change.
   *   variantId (Nhập kho): list is moved under portalRoot (must be the open modal panel,
   *   NOT document.body — body sits under dialog top-layer and stays invisible).
   */
  function wire(form, options) {
    const url = form?.dataset.suggestUrl;
    if (!url || form.dataset.adminSkuSuggestWired === '1') return;

    const mode = options?.mode === 'variantId' ? 'variantId' : 'sku';
    const portalRoot = options?.portalRoot || null;
    const usePortal = mode === 'variantId' && portalRoot instanceof HTMLElement;
    form.dataset.adminSkuSuggestWired = '1';
    let timer = null;

    function homeFor(list) {
      return list._skuSuggestHome || null;
    }

    function ensureHome(list, wrap) {
      if (!list._skuSuggestHome) list._skuSuggestHome = wrap;
    }

    function findList(wrap) {
      return wrap.querySelector(':scope > .sku-suggest__list')
        || Array.from(document.querySelectorAll('.sku-suggest__list--portal'))
          .find(el => homeFor(el) === wrap)
        || null;
    }

    function placePortal(list, input) {
      const rect = input.getBoundingClientRect();
      const rootRect = portalRoot.getBoundingClientRect();
      list.classList.add('sku-suggest__list--portal');
      // position absolute relative to portalRoot (modal panel)
      list.style.position = 'absolute';
      list.style.left = `${Math.round(rect.left - rootRect.left)}px`;
      list.style.top = `${Math.round(rect.bottom - rootRect.top + 2)}px`;
      list.style.width = `${Math.round(rect.width)}px`;
      list.style.right = 'auto';
      list.style.zIndex = '300';
      if (list.parentElement !== portalRoot) portalRoot.appendChild(list);
    }

    function restorePortal(list) {
      list.classList.remove('sku-suggest__list--portal');
      list.style.position = '';
      list.style.left = '';
      list.style.top = '';
      list.style.width = '';
      list.style.right = '';
      list.style.zIndex = '';
      const home = homeFor(list);
      if (home && list.parentElement !== home) home.appendChild(list);
    }

    function hideLists(except) {
      const lists = new Set([
        ...form.querySelectorAll('.sku-suggest__list'),
        ...document.querySelectorAll('.sku-suggest__list--portal')
      ]);
      lists.forEach(list => {
        if (list === except) return;
        if (usePortal && homeFor(list) && !form.contains(homeFor(list))) return;
        list.hidden = true;
        list.innerHTML = '';
        if (usePortal) restorePortal(list);
      });
    }

    function showList(list, wrap, input, html) {
      ensureHome(list, wrap);
      list.innerHTML = html;
      list.removeAttribute('hidden');
      list.hidden = false;
      if (usePortal) placePortal(list, input);
      hideLists(list);
    }

    function applyOption(option, wrap, input, list) {
      if (mode === 'variantId') {
        const idInput = wrap?.querySelector('.js-variant-id');
        if (idInput) idInput.value = option.dataset.id || '';
        if (input) input.value = option.dataset.label || '';
      } else {
        const row = option.closest('.manual-order-line');
        const priceInput = row?.querySelector('.js-sku-price');
        if (input) input.value = option.dataset.sku || '';
        if (priceInput && option.dataset.price !== undefined && option.dataset.price !== '') {
          priceInput.value = String(Math.round(Number(option.dataset.price)));
        }
      }
      if (list) {
        list.hidden = true;
        list.innerHTML = '';
        if (usePortal) restorePortal(list);
      }
    }

    form.addEventListener('input', event => {
      const input = event.target.closest('.js-sku-input');
      if (!input) return;
      const wrap = input.closest('.sku-suggest');
      const list = wrap && findList(wrap);
      if (!wrap || !list) return;

      if (mode === 'variantId') {
        const idInput = wrap.querySelector('.js-variant-id');
        if (idInput) idInput.value = '';
      }

      clearTimeout(timer);
      const query = input.value.trim();
      if (query.length < 1) {
        hideLists();
        return;
      }

      timer = setTimeout(async () => {
        try {
          const response = await fetch(url + '?q=' + encodeURIComponent(query));
          if (!response.ok) throw new Error('suggest http ' + response.status);
          const items = await response.json();
          if (!Array.isArray(items) || items.length === 0) {
            showList(list, wrap, input, '<li class="sku-suggest__empty">Không tìm thấy</li>');
            return;
          }
          const html = items.map(item => {
            const display = item.display ?? item.Display ?? '';
            if (mode === 'variantId') {
              const id = item.id ?? item.Id ?? '';
              return `<li role="option" tabindex="-1" data-id="${escapeAttribute(id)}" data-label="${escapeAttribute(display)}">${escapeHtml(display)}</li>`;
            }
            const sku = item.sku ?? item.Sku ?? '';
            const price = item.price ?? item.Price ?? '';
            return `<li role="option" tabindex="-1" data-sku="${escapeAttribute(sku)}" data-price="${escapeAttribute(price)}">${escapeHtml(display || sku)}</li>`;
          }).join('');
          showList(list, wrap, input, html);
        } catch {
          hideLists();
        }
      }, 220);
    });

    form.addEventListener('click', event => {
      const selector = mode === 'variantId'
        ? '.sku-suggest__list [data-id]'
        : '.sku-suggest__list [data-sku]';
      const option = event.target.closest(selector);
      if (!option) return;
      // Portaled list is outside form — handled below
      if (usePortal && option.closest('.sku-suggest__list--portal')) return;
      event.preventDefault();
      const wrap = option.closest('.sku-suggest');
      const input = wrap?.querySelector('.js-sku-input');
      const list = wrap && findList(wrap);
      applyOption(option, wrap, input, list);
    });

    document.addEventListener('click', event => {
      if (!document.body.contains(form)) return;

      if (usePortal) {
        const option = event.target.closest('.sku-suggest__list--portal [data-id]');
        if (option) {
          const list = option.closest('.sku-suggest__list');
          if (!homeFor(list) || !form.contains(homeFor(list))) return;
          event.preventDefault();
          const wrap = homeFor(list);
          applyOption(option, wrap, wrap?.querySelector('.js-sku-input'), list);
          return;
        }
        if (event.target.closest('.sku-suggest__list--portal')) return;
      }

      if (!form.contains(event.target) || !event.target.closest('.sku-suggest')) {
        hideLists();
      }
    });

    form.addEventListener('focusin', event => {
      if (!event.target.closest('.sku-suggest')) hideLists();
    });
  }

  window.AdminSkuSuggest = { wire };
})(window, document);
