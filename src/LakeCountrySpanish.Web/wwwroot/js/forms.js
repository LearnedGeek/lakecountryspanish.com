/**
 * Form utility for managing form interactions
 */
const Forms = {
    /**
     * Validate a form
     * @param {string} formId - The ID of the form to validate
     * @returns {boolean} - Whether the form is valid
     */
    validate(formId) {
        const form = document.getElementById(formId);
        if (!form) return false;

        // Trigger HTML5 validation
        if (!form.checkValidity()) {
            form.reportValidity();
            return false;
        }

        return true;
    },

    /**
     * Reset a form to its initial state
     * @param {string} formId - The ID of the form to reset
     */
    reset(formId) {
        const form = document.getElementById(formId);
        if (form) {
            form.reset();
            // Clear validation messages
            const validationMessages = form.querySelectorAll('[data-valmsg-for]');
            validationMessages.forEach(msg => msg.textContent = '');
            // Remove validation styling
            const inputs = form.querySelectorAll('.input-validation-error');
            inputs.forEach(input => input.classList.remove('input-validation-error'));
        }
    },

    /**
     * Submit a form asynchronously
     * @param {string} formId - The ID of the form to submit
     * @param {Object} options - Options for the submission
     * @returns {Promise} - Promise resolving to the response
     */
    async submitAsync(formId, options = {}) {
        const form = document.getElementById(formId);
        if (!form) {
            throw new Error(`Form with ID "${formId}" not found`);
        }

        if (!Forms.validate(formId)) {
            throw new Error('Form validation failed');
        }

        const url = options.url || form.action;
        const method = options.method || form.method || 'POST';
        const formData = new FormData(form);

        const fetchOptions = {
            method: method,
            body: formData,
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            }
        };

        // Add anti-forgery token if available
        const token = form.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            fetchOptions.headers['RequestVerificationToken'] = token.value;
        }

        try {
            const response = await fetch(url, fetchOptions);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                return await response.json();
            }

            return await response.text();
        } catch (error) {
            console.error('Form submission error:', error);
            throw error;
        }
    },

    /**
     * Enable/disable form submission
     * @param {string} formId - The ID of the form
     * @param {boolean} enabled - Whether to enable the form
     */
    setEnabled(formId, enabled) {
        const form = document.getElementById(formId);
        if (!form) return;

        const submitButtons = form.querySelectorAll('button[type="submit"], input[type="submit"]');
        submitButtons.forEach(button => {
            button.disabled = !enabled;
            if (enabled) {
                button.classList.remove('opacity-50', 'cursor-not-allowed');
            } else {
                button.classList.add('opacity-50', 'cursor-not-allowed');
            }
        });
    },

    /**
     * Show loading state on form
     * @param {string} formId - The ID of the form
     * @param {boolean} loading - Whether to show loading state
     */
    setLoading(formId, loading) {
        Forms.setEnabled(formId, !loading);

        const form = document.getElementById(formId);
        if (!form) return;

        const submitButtons = form.querySelectorAll('button[type="submit"]');
        submitButtons.forEach(button => {
            if (loading) {
                button.dataset.originalText = button.innerHTML;
                button.innerHTML = `
                    <svg class="animate-spin -ml-1 mr-2 h-4 w-4 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                        <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                        <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                    </svg>
                    Processing...
                `;
            } else if (button.dataset.originalText) {
                button.innerHTML = button.dataset.originalText;
                delete button.dataset.originalText;
            }
        });
    }
};
