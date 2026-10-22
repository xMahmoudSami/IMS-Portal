// Inventory Management System - Global Client Scripts

/**
 * Safe print trigger utility.
 * Focuses the window and invokes window.print() asynchronously to decouple
 * from the click event handler, preventing browser UI freezes and avoiding
 * Visual Studio debugger detachment / CDP disconnect issues.
 * 
 * @param {string|Event} [customTitleOrEvent] - Optional title to set before printing (e.g., 'INV-00008') or Event object.
 * @param {Event} [evt] - Optional click event object.
 */
function triggerPrint(customTitleOrEvent, evt) {
    var title = null;
    var eventObj = null;

    if (customTitleOrEvent) {
        if (typeof customTitleOrEvent === 'string') {
            title = customTitleOrEvent;
            eventObj = evt;
        } else if (typeof customTitleOrEvent === 'object' && (customTitleOrEvent instanceof Event || customTitleOrEvent.target)) {
            eventObj = customTitleOrEvent;
        }
    }

    if (eventObj && typeof eventObj.preventDefault === 'function') {
        try {
            eventObj.preventDefault();
            if (typeof eventObj.stopPropagation === 'function') {
                eventObj.stopPropagation();
            }
        } catch (e) {
            // Ignore synthetic event errors
        }
    }

    var originalTitle = document.title;
    if (title && typeof title === 'string' && title.trim() !== '') {
        document.title = title.trim();
    }

    // Restore title after print dialog closes
    var restoreTitleHandler = function () {
        window.removeEventListener('afterprint', restoreTitleHandler);
        if (originalTitle && title && document.title === title.trim()) {
            document.title = originalTitle;
        }
    };
    window.addEventListener('afterprint', restoreTitleHandler);

    // Asynchronously decouple from click event loop so the handler returns immediately
    // and doesn't block the browser UI thread or debugger connection
    if (typeof window.requestAnimationFrame === 'function') {
        window.requestAnimationFrame(function () {
            setTimeout(executePrint, 150);
        });
    } else {
        setTimeout(executePrint, 150);
    }

    function executePrint() {
        try {
            window.focus();
            window.print();
        } catch (err) {
            console.error('Safe print trigger encountered an error:', err);
        }
    }
}

// Expose globally on window
window.triggerPrint = triggerPrint;

// Synchronize document.title for browser print dialogs if a print-ready element defines data-print-title
window.addEventListener('beforeprint', function () {
    try {
        var printEl = document.querySelector('[data-print-title]');
        if (printEl) {
            var customTitle = printEl.getAttribute('data-print-title');
            if (customTitle && customTitle.trim() !== '') {
                document.title = customTitle.trim();
            }
        }
    } catch (e) {
        console.warn('Could not set custom print title:', e);
    }
});

/**
 * Modern Custom Dropdown Component
 * Seamlessly replaces native <select> elements with an accessible, fully themed custom dropdown
 * that eliminates browser-specific / OS-native blue hover highlights and matches the Warm Luxury theme.
 */
(function ($) {
    'use strict';

    function initCustomSelect(select) {
        var $select = $(select);

        // Skip if already initialized, multiple, template, or hidden
        if ($select.hasClass('ims-select-initialized') ||
            $select.is('[multiple]') ||
            $select.is('.no-custom-select') ||
            $select.is('#productOptionsTemplate') ||
            $select.closest('#productOptionsTemplate').length > 0 ||
            $select.hasClass('d-none') ||
            ($select.is(':hidden') && !$select.hasClass('ims-select-native-hidden'))) {
            return;
        }

        $select.addClass('ims-select-initialized ims-select-native-hidden');

        // Extract sizing classes
        var isSmall = $select.hasClass('custom-select-sm') || $select.hasClass('form-control-sm');
        var wrapperClass = 'ims-select-wrapper' + (isSmall ? ' ims-select-sm' : '');

        var $wrapper = $('<div class="' + wrapperClass + '"></div>');
        var $toggle = $('<button type="button" class="ims-select-toggle" aria-haspopup="listbox" aria-expanded="false"></button>');
        var $label = $('<span class="ims-select-label"></span>');
        var $arrow = $('<i class="fas fa-chevron-down ims-select-arrow"></i>');
        var $menu = $('<div class="ims-select-menu" role="listbox"></div>');
        var $optionsList = $('<div class="ims-select-options"></div>');

        if ($select.is(':disabled')) {
            $toggle.attr('disabled', 'disabled').addClass('disabled');
        }

        $toggle.append($label).append($arrow);
        $menu.append($optionsList);
        $wrapper.append($toggle).append($menu);

        // Insert wrapper right after native select
        $select.after($wrapper);

        function renderOptions() {
            $optionsList.empty();
            var selectedText = '';
            var selectedVal = $select.val();

            $select.find('option').each(function () {
                var $opt = $(this);
                var val = $opt.val();
                var text = $opt.text();
                var isSelected = $opt.is(':selected') || (val !== undefined && val !== null && String(val) === String(selectedVal));

                var $item = $('<div class="ims-select-option" role="option" data-value="' + val + '"></div>');
                $item.text(text);

                if (isSelected) {
                    $item.addClass('selected');
                    $item.append('<i class="fas fa-check ims-select-check"></i>');
                    selectedText = text;
                }

                if ($opt.is(':disabled')) {
                    $item.addClass('disabled');
                }

                $optionsList.append($item);
            });

            if (!selectedText) {
                var $firstOpt = $select.find('option').first();
                selectedText = $firstOpt.length ? $firstOpt.text() : '';
            }

            $label.text(selectedText);
        }

        renderOptions();

        // Toggle dropdown open/close
        $toggle.on('click', function (e) {
            e.preventDefault();
            e.stopPropagation();

            if ($select.is(':disabled') || $toggle.is(':disabled')) return;

            var isOpen = $wrapper.hasClass('open');
            $('.ims-select-wrapper.open').not($wrapper).removeClass('open').find('.ims-select-toggle').attr('aria-expanded', 'false');

            if (!isOpen) {
                // Determine if dropup is needed
                var toggleRect = $toggle[0].getBoundingClientRect();
                var spaceBelow = window.innerHeight - toggleRect.bottom;
                if (spaceBelow < 220 && toggleRect.top > 220) {
                    $wrapper.addClass('dropup');
                } else {
                    $wrapper.removeClass('dropup');
                }

                $wrapper.addClass('open');
                $toggle.attr('aria-expanded', 'true');

                // Scroll active option into view and set keyboard focus
                $optionsList.find('.ims-select-option').removeClass('focused');
                var $active = $optionsList.find('.ims-select-option.selected');
                if ($active.length) {
                    $active.addClass('focused');
                    var menuHeight = $menu.innerHeight();
                    var activePos = $active.position().top;
                    if (activePos < 0 || activePos > menuHeight) {
                        $menu.scrollTop($menu.scrollTop() + activePos - 30);
                    }
                }
            } else {
                $wrapper.removeClass('open');
                $toggle.attr('aria-expanded', 'false');
            }
        });

        // Option click handler
        $optionsList.on('click', '.ims-select-option:not(.disabled)', function (e) {
            e.preventDefault();
            e.stopPropagation();

            var $opt = $(this);
            var val = $opt.attr('data-value');

            $optionsList.find('.ims-select-option').removeClass('selected focused').find('.ims-select-check').remove();
            $opt.addClass('selected').append('<i class="fas fa-check ims-select-check"></i>');

            $label.text($opt.clone().children().remove().end().text().trim());
            $wrapper.removeClass('open');
            $toggle.attr('aria-expanded', 'false').focus();

            // Sync with native select
            if ($select.val() !== val) {
                $select.val(val);
                $select.trigger('change');
                var nativeEl = $select.get(0);
                if (nativeEl) {
                    var evt = new Event('change', { bubbles: true });
                    nativeEl.dispatchEvent(evt);
                    if (typeof nativeEl.onchange === 'function') {
                        nativeEl.onchange();
                    }
                }
                if (typeof $select.valid === 'function') {
                    $select.valid();
                }
            }
        });

        // Sync when native select is changed by outside code
        $select.on('change', function () {
            var currentVal = $select.val();
            $optionsList.find('.ims-select-option').removeClass('selected focused').find('.ims-select-check').remove();
            var $matching = $optionsList.find('.ims-select-option').filter(function () {
                return String($(this).attr('data-value')) === String(currentVal);
            });
            if ($matching.length) {
                $matching.addClass('selected').append('<i class="fas fa-check ims-select-check"></i>');
                $label.text($matching.clone().children().remove().end().text().trim());
            } else {
                var $selOpt = $select.find('option:selected');
                if ($selOpt.length) {
                    $label.text($selOpt.text());
                }
            }
        });

        // Sync disabled state
        if (window.MutationObserver) {
            var selectObserver = new MutationObserver(function (mutations) {
                var shouldRerender = false;
                for (var i = 0; i < mutations.length; i++) {
                    if (mutations[i].type === 'childList') {
                        shouldRerender = true;
                        break;
                    } else if (mutations[i].attributeName === 'disabled') {
                        if ($select.is(':disabled')) {
                            $toggle.attr('disabled', 'disabled').addClass('disabled');
                        } else {
                            $toggle.removeAttr('disabled').removeClass('disabled');
                        }
                    }
                }
                if (shouldRerender) {
                    renderOptions();
                }
            });
            selectObserver.observe(select, { childList: true, attributes: true, attributeFilter: ['disabled'] });
        }

        // Keyboard navigation
        $toggle.on('keydown', function (e) {
            if ($select.is(':disabled') || $toggle.is(':disabled')) return;

            if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
                e.preventDefault();
                if (!$wrapper.hasClass('open')) {
                    $toggle.trigger('click');
                    return;
                }
                var $options = $optionsList.find('.ims-select-option:not(.disabled)');
                var $current = $options.filter('.focused');
                if (!$current.length) {
                    $current = $options.filter('.selected');
                }
                var index = $options.index($current);
                if (e.key === 'ArrowDown') {
                    index = (index + 1) % $options.length;
                } else {
                    index = (index - 1 + $options.length) % $options.length;
                }
                $options.removeClass('focused');
                var $next = $options.eq(index).addClass('focused');
                var optEl = $next[0];
                if (optEl && typeof optEl.scrollIntoView === 'function') {
                    optEl.scrollIntoView({ block: 'nearest' });
                }
            } else if (e.key === 'Enter' || e.key === ' ') {
                if ($wrapper.hasClass('open')) {
                    var $focused = $optionsList.find('.ims-select-option.focused');
                    if ($focused.length) {
                        e.preventDefault();
                        $focused.trigger('click');
                    }
                } else {
                    e.preventDefault();
                    $toggle.trigger('click');
                }
            } else if (e.key === 'Escape' || e.key === 'Tab') {
                $wrapper.removeClass('open');
                $toggle.attr('aria-expanded', 'false');
            }
        });
    }

    function initAllCustomSelects() {
        $('select.custom-select, select.form-control').each(function () {
            initCustomSelect(this);
        });
    }

    // Close on click outside
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.ims-select-wrapper').length) {
            $('.ims-select-wrapper.open').removeClass('open').find('.ims-select-toggle').attr('aria-expanded', 'false');
        }
    });

    // Expose on window for manual calls if needed
    window.initCustomSelects = initAllCustomSelects;

    // Run on ready
    $(function () {
        initAllCustomSelects();

        // Observe DOM for dynamic rows (e.g. Purchases and Sales item rows)
        if (window.MutationObserver) {
            var bodyObserver = new MutationObserver(function (mutations) {
                var foundNewSelect = false;
                for (var i = 0; i < mutations.length; i++) {
                    if (mutations[i].addedNodes && mutations[i].addedNodes.length > 0) {
                        for (var j = 0; j < mutations[i].addedNodes.length; j++) {
                            var node = mutations[i].addedNodes[j];
                            if (node.nodeType === 1) { // ELEMENT_NODE
                                if ($(node).is('select.custom-select, select.form-control') || $(node).find('select.custom-select, select.form-control').length > 0) {
                                    foundNewSelect = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (foundNewSelect) break;
                }
                if (foundNewSelect) {
                    initAllCustomSelects();
                }
            });
            bodyObserver.observe(document.body, { childList: true, subtree: true });
        }
    });
})(jQuery);

