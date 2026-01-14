/**
 * Animation Service for Lake Country Spanish Gamification
 * Uses GSAP, Lottie, and Canvas Confetti for animations
 */
const AnimationService = (function () {
    'use strict';

    // Animation file paths
    const ANIMATION_PATH = '/animations/';

    // Check for reduced motion preference
    function prefersReducedMotion() {
        return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    /**
     * Fire confetti celebration
     * @param {object} options - Confetti options
     */
    function fireConfetti(options = {}) {
        if (prefersReducedMotion()) return;

        const defaults = {
            particleCount: 100,
            spread: 70,
            origin: { y: 0.6 },
            colors: ['#1e3a5f', '#e85a42', '#f59e0b', '#22c55e'] // Primary, secondary, accent, green
        };

        const settings = { ...defaults, ...options };

        if (typeof confetti === 'function') {
            confetti(settings);
        }
    }

    /**
     * Fire celebration confetti from both sides
     */
    function fireCelebration() {
        if (prefersReducedMotion()) return;

        const count = 200;
        const defaults = {
            origin: { y: 0.7 },
            colors: ['#1e3a5f', '#e85a42', '#f59e0b', '#22c55e', '#ffffff']
        };

        function fire(particleRatio, opts) {
            confetti({
                ...defaults,
                ...opts,
                particleCount: Math.floor(count * particleRatio)
            });
        }

        fire(0.25, { spread: 26, startVelocity: 55 });
        fire(0.2, { spread: 60 });
        fire(0.35, { spread: 100, decay: 0.91, scalar: 0.8 });
        fire(0.1, { spread: 120, startVelocity: 25, decay: 0.92, scalar: 1.2 });
        fire(0.1, { spread: 120, startVelocity: 45 });
    }

    /**
     * Fire star burst effect
     */
    function fireStarBurst() {
        if (prefersReducedMotion()) return;

        const defaults = {
            spread: 360,
            ticks: 50,
            gravity: 0,
            decay: 0.94,
            startVelocity: 30,
            shapes: ['star'],
            colors: ['#f59e0b', '#fcd34d', '#fef3c7']
        };

        confetti({
            ...defaults,
            particleCount: 40,
            scalar: 1.2,
            shapes: ['star']
        });

        confetti({
            ...defaults,
            particleCount: 10,
            scalar: 0.75,
            shapes: ['circle']
        });
    }

    /**
     * Animate element with GSAP pulse effect
     * @param {Element|string} element - DOM element or selector
     */
    function pulse(element) {
        if (prefersReducedMotion()) return;

        if (typeof gsap !== 'undefined') {
            gsap.fromTo(element,
                { scale: 1 },
                {
                    scale: 1.1,
                    duration: 0.15,
                    yoyo: true,
                    repeat: 1,
                    ease: 'power2.out'
                }
            );
        }
    }

    /**
     * Animate element with bounce effect
     * @param {Element|string} element - DOM element or selector
     */
    function bounce(element) {
        if (prefersReducedMotion()) return;

        if (typeof gsap !== 'undefined') {
            gsap.fromTo(element,
                { y: 0 },
                {
                    y: -10,
                    duration: 0.2,
                    yoyo: true,
                    repeat: 1,
                    ease: 'power2.out'
                }
            );
        }
    }

    /**
     * Animate element with shake effect (for wrong answers)
     * @param {Element|string} element - DOM element or selector
     */
    function shake(element) {
        if (prefersReducedMotion()) return;

        if (typeof gsap !== 'undefined') {
            gsap.fromTo(element,
                { x: 0 },
                {
                    x: 5,
                    duration: 0.08,
                    yoyo: true,
                    repeat: 5,
                    ease: 'power2.inOut'
                }
            );
        }
    }

    /**
     * Animate element with pop-in effect
     * @param {Element|string} element - DOM element or selector
     * @param {object} options - Animation options
     */
    function popIn(element, options = {}) {
        if (prefersReducedMotion()) {
            gsap.set(element, { opacity: 1, scale: 1 });
            return;
        }

        const defaults = {
            duration: 0.4,
            delay: 0,
            ease: 'back.out(1.7)'
        };

        const settings = { ...defaults, ...options };

        if (typeof gsap !== 'undefined') {
            gsap.fromTo(element,
                { opacity: 0, scale: 0.5 },
                {
                    opacity: 1,
                    scale: 1,
                    duration: settings.duration,
                    delay: settings.delay,
                    ease: settings.ease
                }
            );
        }
    }

    /**
     * Animate element sliding in from bottom
     * @param {Element|string} element - DOM element or selector
     * @param {object} options - Animation options
     */
    function slideUp(element, options = {}) {
        if (prefersReducedMotion()) {
            gsap.set(element, { opacity: 1, y: 0 });
            return;
        }

        const defaults = {
            duration: 0.5,
            delay: 0,
            distance: 30,
            ease: 'power3.out'
        };

        const settings = { ...defaults, ...options };

        if (typeof gsap !== 'undefined') {
            gsap.fromTo(element,
                { opacity: 0, y: settings.distance },
                {
                    opacity: 1,
                    y: 0,
                    duration: settings.duration,
                    delay: settings.delay,
                    ease: settings.ease
                }
            );
        }
    }

    /**
     * Animate number counting up
     * @param {Element|string} element - DOM element or selector
     * @param {number} startValue - Starting value
     * @param {number} endValue - Ending value
     * @param {object} options - Animation options
     */
    function countUp(element, startValue, endValue, options = {}) {
        const defaults = {
            duration: 1,
            ease: 'power2.out',
            prefix: '',
            suffix: ''
        };

        const settings = { ...defaults, ...options };
        const el = typeof element === 'string' ? document.querySelector(element) : element;

        if (!el) return;

        if (prefersReducedMotion()) {
            el.textContent = settings.prefix + endValue + settings.suffix;
            return;
        }

        if (typeof gsap !== 'undefined') {
            const obj = { value: startValue };
            gsap.to(obj, {
                value: endValue,
                duration: settings.duration,
                ease: settings.ease,
                onUpdate: function () {
                    el.textContent = settings.prefix + Math.round(obj.value) + settings.suffix;
                }
            });
        }
    }

    /**
     * Add ripple effect to element
     * @param {Element} element - DOM element to add ripple to
     * @param {Event} event - Click event for positioning
     */
    function ripple(element, event) {
        if (prefersReducedMotion()) return;

        const ripple = document.createElement('span');
        ripple.classList.add('gamification-ripple');

        const rect = element.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height);
        const x = event.clientX - rect.left - size / 2;
        const y = event.clientY - rect.top - size / 2;

        ripple.style.width = ripple.style.height = `${size}px`;
        ripple.style.left = `${x}px`;
        ripple.style.top = `${y}px`;

        element.appendChild(ripple);

        setTimeout(() => ripple.remove(), 600);
    }

    /**
     * Create a Lottie animation player
     * @param {string} container - Container selector
     * @param {string} animationName - Name of the animation file
     * @param {object} options - Lottie options
     * @returns {Element} The lottie-player element
     */
    function createLottiePlayer(container, animationName, options = {}) {
        const containerEl = typeof container === 'string' ?
            document.querySelector(container) : container;

        if (!containerEl) return null;

        const player = document.createElement('lottie-player');
        player.setAttribute('src', ANIMATION_PATH + animationName + '.json');
        player.setAttribute('background', 'transparent');
        player.setAttribute('speed', options.speed || '1');
        player.style.width = options.width || '100px';
        player.style.height = options.height || '100px';

        if (options.loop) {
            player.setAttribute('loop', '');
        }

        if (options.autoplay !== false) {
            player.setAttribute('autoplay', '');
        }

        containerEl.appendChild(player);
        return player;
    }

    /**
     * Play a one-shot Lottie animation overlay
     * @param {string} animationName - Name of the animation
     * @param {object} options - Display options
     */
    function playLottieOverlay(animationName, options = {}) {
        if (prefersReducedMotion()) return;

        const overlay = document.createElement('div');
        overlay.className = 'lottie-overlay';
        overlay.style.cssText = `
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            z-index: 9999;
            pointer-events: none;
        `;

        document.body.appendChild(overlay);

        const player = createLottiePlayer(overlay, animationName, {
            width: options.size || '200px',
            height: options.size || '200px',
            autoplay: true,
            loop: false
        });

        if (player) {
            player.addEventListener('complete', () => {
                overlay.remove();
            });

            // Fallback removal after 3 seconds
            setTimeout(() => overlay.remove(), 3000);
        }
    }

    /**
     * Show checkmark animation
     */
    function showCheckmark() {
        playLottieOverlay('checkmark', { size: '150px' });
    }

    /**
     * Show wrong X animation
     */
    function showWrongX() {
        playLottieOverlay('wrong-x', { size: '150px' });
    }

    /**
     * Show star burst animation
     */
    function showStarBurst() {
        playLottieOverlay('star-burst', { size: '200px' });
    }

    /**
     * Show trophy animation
     */
    function showTrophy() {
        playLottieOverlay('trophy', { size: '200px' });
    }

    // Public API
    return {
        // Confetti effects
        fireConfetti,
        fireCelebration,
        fireStarBurst,

        // GSAP animations
        pulse,
        bounce,
        shake,
        popIn,
        slideUp,
        countUp,
        ripple,

        // Lottie animations
        createLottiePlayer,
        playLottieOverlay,
        showCheckmark,
        showWrongX,
        showStarBurst,
        showTrophy,

        // Utilities
        prefersReducedMotion
    };
})();

// Make available globally
window.AnimationService = AnimationService;
