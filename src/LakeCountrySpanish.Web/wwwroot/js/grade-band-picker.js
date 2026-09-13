// Grade-band chip picker (issue #19 + #20).
//
// Renders across two admin surfaces (Program form + Binder upload) via
// the _GradeBandPicker.cshtml partial. Behavior:
//   - Click an unselected chip: select it AND arm it as the range anchor
//   - Click a second chip: fill the contiguous range between anchor and it
//   - Click an already-selected chip (with no anchor armed): toggle it off
//
// Kept dependency-free — no framework, no build step. Selection state
// lives on `data-band-selected` for the chip and the `disabled` attribute
// on the paired hidden input (disabled inputs don't post, which is how
// unselected bands are excluded from the form submit).
(function () {
    'use strict';

    function setChip(el, selected) {
        el.dataset.bandSelected = selected ? 'true' : 'false';
        el.setAttribute('aria-pressed', selected ? 'true' : 'false');
        if (selected) {
            el.classList.add('border-indigo-500', 'bg-indigo-100', 'text-indigo-800');
            el.classList.remove('border-gray-300', 'bg-white', 'text-gray-700', 'hover:border-gray-400');
        } else {
            el.classList.remove('border-indigo-500', 'bg-indigo-100', 'text-indigo-800');
            el.classList.add('border-gray-300', 'bg-white', 'text-gray-700', 'hover:border-gray-400');
        }
        const scope = el.closest('#grade-band-chips') || document;
        const hidden = scope.querySelector('.grade-hidden-input[data-band-value="' + el.dataset.bandValue + '"]');
        if (hidden) hidden.disabled = !selected;
    }

    function initChipContainer(container) {
        if (!container || container.dataset.gradePickerInitialized === 'true') return;
        container.dataset.gradePickerInitialized = 'true';

        const chips = Array.from(container.querySelectorAll('.grade-chip'));
        let pendingStart = null;

        chips.forEach(function (chip) {
            chip.addEventListener('click', function () {
                const value = Number.parseInt(chip.dataset.bandValue, 10);
                const currentlySelected = chip.dataset.bandSelected === 'true';

                if (pendingStart === null) {
                    if (currentlySelected) {
                        setChip(chip, false);
                    } else {
                        setChip(chip, true);
                        pendingStart = value;
                    }
                    return;
                }

                const lo = Math.min(pendingStart, value);
                const hi = Math.max(pendingStart, value);
                chips.forEach(function (c) {
                    const v = Number.parseInt(c.dataset.bandValue, 10);
                    if (v >= lo && v <= hi) setChip(c, true);
                });
                pendingStart = null;
            });
        });
    }

    function initAll() {
        document.querySelectorAll('#grade-band-chips').forEach(initChipContainer);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initAll);
    } else {
        initAll();
    }
})();
