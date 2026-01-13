/**
 * Modal utility for managing modal dialogs
 */
const Modal = {
    /**
     * Open a modal by ID
     * @param {string} modalId - The ID of the modal to open
     */
    open(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.classList.remove('hidden');
            document.body.classList.add('overflow-hidden');
            // Focus first focusable element
            const focusable = modal.querySelector('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
            if (focusable) {
                focusable.focus();
            }
            // Trap focus within modal
            modal.addEventListener('keydown', Modal._trapFocus);
        }
    },

    /**
     * Close a modal by ID
     * @param {string} modalId - The ID of the modal to close
     */
    close(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            modal.classList.add('hidden');
            document.body.classList.remove('overflow-hidden');
            modal.removeEventListener('keydown', Modal._trapFocus);
        }
    },

    /**
     * Toggle a modal's visibility
     * @param {string} modalId - The ID of the modal to toggle
     */
    toggle(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            if (modal.classList.contains('hidden')) {
                Modal.open(modalId);
            } else {
                Modal.close(modalId);
            }
        }
    },

    /**
     * Reset form within a modal
     * @param {string} modalId - The ID of the modal containing the form
     */
    reset(modalId) {
        const modal = document.getElementById(modalId);
        if (modal) {
            const forms = modal.querySelectorAll('form');
            forms.forEach(form => form.reset());
        }
    },

    /**
     * Close modal on Escape key
     * @param {KeyboardEvent} event
     */
    _handleEscape(event) {
        if (event.key === 'Escape') {
            const visibleModals = document.querySelectorAll('[role="dialog"]:not(.hidden)');
            visibleModals.forEach(modal => {
                Modal.close(modal.id);
            });
        }
    },

    /**
     * Trap focus within modal
     * @param {KeyboardEvent} event
     */
    _trapFocus(event) {
        if (event.key !== 'Tab') return;

        const modal = event.currentTarget;
        const focusableElements = modal.querySelectorAll(
            'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
        );
        const firstElement = focusableElements[0];
        const lastElement = focusableElements[focusableElements.length - 1];

        if (event.shiftKey && document.activeElement === firstElement) {
            event.preventDefault();
            lastElement.focus();
        } else if (!event.shiftKey && document.activeElement === lastElement) {
            event.preventDefault();
            firstElement.focus();
        }
    },

    /**
     * Initialize modal event listeners
     */
    init() {
        document.addEventListener('keydown', Modal._handleEscape);
    }
};

// Initialize on DOM ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => Modal.init());
} else {
    Modal.init();
}
