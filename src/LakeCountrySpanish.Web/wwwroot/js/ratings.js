/**
 * Star rating component utility
 */
const Ratings = {
    /**
     * Initialize a star rating component
     * @param {string} containerId - The ID of the container element
     * @param {Object} options - Configuration options
     */
    init(containerId, options = {}) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const config = {
            maxStars: options.maxStars || 5,
            initialValue: options.initialValue || 0,
            readOnly: options.readOnly || false,
            inputName: options.inputName || 'rating',
            size: options.size || 'md',
            onChange: options.onChange || null
        };

        container.dataset.ratingConfig = JSON.stringify(config);
        Ratings._render(container, config);
    },

    /**
     * Render the star rating UI
     * @private
     */
    _render(container, config) {
        const sizeClasses = {
            sm: 'h-4 w-4',
            md: 'h-6 w-6',
            lg: 'h-8 w-8'
        };

        const starSize = sizeClasses[config.size] || sizeClasses.md;

        let html = `<input type="hidden" name="${config.inputName}" value="${config.initialValue}" />`;
        html += '<div class="flex items-center space-x-1">';

        for (let i = 1; i <= config.maxStars; i++) {
            const isFilled = i <= config.initialValue;
            const cursorClass = config.readOnly ? '' : 'cursor-pointer';
            const hoverClass = config.readOnly ? '' : 'hover:text-yellow-400';
            const colorClass = isFilled ? 'text-yellow-400' : 'text-gray-300';

            html += `
                <button type="button"
                        class="rating-star ${cursorClass} ${hoverClass} ${colorClass} focus:outline-none transition-colors"
                        data-value="${i}"
                        ${config.readOnly ? 'disabled' : ''}>
                    <svg class="${starSize}" fill="currentColor" viewBox="0 0 24 24">
                        <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
                    </svg>
                </button>
            `;
        }

        html += '</div>';
        container.innerHTML = html;

        if (!config.readOnly) {
            Ratings._attachEvents(container, config);
        }
    },

    /**
     * Attach event listeners
     * @private
     */
    _attachEvents(container, config) {
        const stars = container.querySelectorAll('.rating-star');
        const input = container.querySelector('input[type="hidden"]');

        stars.forEach(star => {
            star.addEventListener('click', () => {
                const value = parseInt(star.dataset.value);
                Ratings.setValue(container.id, value);

                if (config.onChange) {
                    config.onChange(value);
                }
            });

            star.addEventListener('mouseenter', () => {
                const value = parseInt(star.dataset.value);
                Ratings._highlightStars(container, value);
            });
        });

        container.addEventListener('mouseleave', () => {
            const currentValue = parseInt(input.value) || 0;
            Ratings._highlightStars(container, currentValue);
        });
    },

    /**
     * Highlight stars up to a given value
     * @private
     */
    _highlightStars(container, value) {
        const stars = container.querySelectorAll('.rating-star');
        stars.forEach((star, index) => {
            if (index < value) {
                star.classList.remove('text-gray-300');
                star.classList.add('text-yellow-400');
            } else {
                star.classList.remove('text-yellow-400');
                star.classList.add('text-gray-300');
            }
        });
    },

    /**
     * Set the rating value
     * @param {string} containerId - The ID of the container
     * @param {number} value - The rating value (1-5)
     */
    setValue(containerId, value) {
        const container = document.getElementById(containerId);
        if (!container) return;

        const input = container.querySelector('input[type="hidden"]');
        if (input) {
            input.value = value;
        }

        Ratings._highlightStars(container, value);
    },

    /**
     * Get the current rating value
     * @param {string} containerId - The ID of the container
     * @returns {number} - The current rating value
     */
    getValue(containerId) {
        const container = document.getElementById(containerId);
        if (!container) return 0;

        const input = container.querySelector('input[type="hidden"]');
        return input ? parseInt(input.value) || 0 : 0;
    },

    /**
     * Create a read-only star display
     * @param {number} value - The rating value to display
     * @param {string} size - Size class (sm, md, lg)
     * @returns {string} - HTML string for the star display
     */
    createReadOnly(value, size = 'md') {
        const sizeClasses = {
            sm: 'h-4 w-4',
            md: 'h-5 w-5',
            lg: 'h-6 w-6'
        };

        const starSize = sizeClasses[size] || sizeClasses.md;
        let html = '<div class="flex items-center">';

        for (let i = 1; i <= 5; i++) {
            const colorClass = i <= value ? 'text-yellow-400' : 'text-gray-300';
            html += `
                <svg class="${starSize} ${colorClass}" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z" />
                </svg>
            `;
        }

        html += '</div>';
        return html;
    }
};
