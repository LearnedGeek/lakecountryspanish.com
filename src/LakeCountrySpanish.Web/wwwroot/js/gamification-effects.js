/**
 * Gamification Effects for Lake Country Spanish
 * High-level API combining sounds, animations, and toasts
 * Provides Duolingo-style feedback for student interactions
 */
const GamificationEffects = (function () {
    'use strict';

    // Toast styling configurations
    const TOAST_STYLES = {
        success: {
            background: 'linear-gradient(to right, #22c55e, #16a34a)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"/></svg>'
        },
        error: {
            background: 'linear-gradient(to right, #ef4444, #dc2626)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"/></svg>'
        },
        warning: {
            background: 'linear-gradient(to right, #f59e0b, #d97706)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>'
        },
        info: {
            background: 'linear-gradient(to right, #3b82f6, #2563eb)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>'
        },
        points: {
            background: 'linear-gradient(to right, #f59e0b, #fbbf24)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 8c-1.657 0-3 .895-3 2s1.343 2 3 2 3 .895 3 2-1.343 2-3 2m0-8c1.11 0 2.08.402 2.599 1M12 8V7m0 1v8m0 0v1m0-1c-1.11 0-2.08-.402-2.599-1M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>'
        },
        streak: {
            background: 'linear-gradient(to right, #e85a42, #f0806b)',
            icon: '<svg class="w-5 h-5 mr-2 inline" viewBox="0 0 24 24" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>'
        },
        badge: {
            background: 'linear-gradient(to right, #8b5cf6, #7c3aed)',
            icon: '<svg class="w-5 h-5 mr-2 inline" fill="none" viewBox="0 0 24 24" stroke="currentColor"><path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4M7.835 4.697a3.42 3.42 0 001.946-.806 3.42 3.42 0 014.438 0 3.42 3.42 0 001.946.806 3.42 3.42 0 013.138 3.138 3.42 3.42 0 00.806 1.946 3.42 3.42 0 010 4.438 3.42 3.42 0 00-.806 1.946 3.42 3.42 0 01-3.138 3.138 3.42 3.42 0 00-1.946.806 3.42 3.42 0 01-4.438 0 3.42 3.42 0 00-1.946-.806 3.42 3.42 0 01-3.138-3.138 3.42 3.42 0 00-.806-1.946 3.42 3.42 0 010-4.438 3.42 3.42 0 00.806-1.946 3.42 3.42 0 013.138-3.138z"/></svg>'
        }
    };

    /**
     * Show a toast notification
     * @param {string} message - Message to display
     * @param {string} type - Toast type (success, error, warning, info, points, streak, badge)
     * @param {object} options - Additional options
     */
    function showToast(message, type = 'info', options = {}) {
        if (typeof Toastify === 'undefined') {
            console.log('Toast:', message);
            return;
        }

        const style = TOAST_STYLES[type] || TOAST_STYLES.info;
        const defaults = {
            text: style.icon + message,
            duration: 3000,
            gravity: 'top',
            position: 'right',
            escapeMarkup: false,
            className: `gamification-toast gamification-toast-${type}`,
            style: {
                background: style.background,
                borderRadius: '8px',
                padding: '12px 20px',
                fontSize: '14px',
                fontWeight: '500',
                boxShadow: '0 10px 25px rgba(0,0,0,0.2)',
                display: 'flex',
                alignItems: 'center'
            },
            offset: {
                y: 20
            }
        };

        const settings = { ...defaults, ...options };
        Toastify(settings).showToast();
    }

    /**
     * Handle correct answer feedback
     * @param {Element} element - The answer element (optional)
     * @param {object} options - Feedback options
     */
    function onCorrectAnswer(element, options = {}) {
        // Play sound
        if (window.SoundService) {
            SoundService.playCorrect();
        }

        // Animate element
        if (element && window.AnimationService) {
            AnimationService.pulse(element);
        }

        // Show toast if message provided
        if (options.message) {
            showToast(options.message, 'success');
        }

        // Show points if provided
        if (options.points) {
            setTimeout(() => {
                showPointsEarned(options.points);
            }, 300);
        }
    }

    /**
     * Handle wrong answer feedback
     * @param {Element} element - The answer element (optional)
     * @param {object} options - Feedback options
     */
    function onWrongAnswer(element, options = {}) {
        // Play sound
        if (window.SoundService) {
            SoundService.playWrong();
        }

        // Animate element
        if (element && window.AnimationService) {
            AnimationService.shake(element);
        }

        // Show toast if message provided
        if (options.message) {
            showToast(options.message, 'error');
        }
    }

    /**
     * Show points earned notification
     * @param {number} points - Number of points earned
     * @param {string} reason - Optional reason for points
     */
    function showPointsEarned(points, reason = '') {
        if (window.SoundService) {
            SoundService.playPoints();
        }

        const message = reason ? `+${points} points - ${reason}` : `+${points} points!`;
        showToast(message, 'points', { duration: 2500 });
    }

    /**
     * Show streak notification
     * @param {number} streak - Current streak count
     */
    function showStreak(streak) {
        if (window.SoundService) {
            SoundService.playStreak();
        }

        if (window.AnimationService) {
            AnimationService.fireConfetti({
                particleCount: 30,
                spread: 60,
                colors: ['#e85a42', '#f0806b', '#f59e0b']
            });
        }

        showToast(`${streak} day streak! Keep it up!`, 'streak', { duration: 4000 });
    }

    /**
     * Show streak milestone celebration
     * @param {number} milestone - Streak milestone (7, 30, 100, etc.)
     */
    function showStreakMilestone(milestone) {
        if (window.SoundService) {
            SoundService.playLevelUp();
        }

        if (window.AnimationService) {
            AnimationService.fireCelebration();
        }

        const messages = {
            7: 'One week streak! Amazing dedication!',
            14: 'Two weeks strong! You\'re on fire!',
            30: 'One month streak! Incredible!',
            60: 'Two months! You\'re unstoppable!',
            90: '90 day streak! Spanish master in the making!',
            100: '100 DAYS! Legendary achievement!',
            365: 'ONE YEAR STREAK! Absolutely incredible!'
        };

        const message = messages[milestone] || `${milestone} day streak milestone!`;
        showToast(message, 'streak', { duration: 5000 });
    }

    /**
     * Show badge earned notification
     * @param {string} badgeName - Name of the badge
     * @param {string} description - Badge description
     */
    function showBadgeEarned(badgeName, description = '') {
        if (window.SoundService) {
            SoundService.playBadge();
        }

        if (window.AnimationService) {
            AnimationService.fireStarBurst();
        }

        const message = description ?
            `Badge earned: ${badgeName} - ${description}` :
            `Badge earned: ${badgeName}!`;

        showToast(message, 'badge', { duration: 5000 });
    }

    /**
     * Show assignment/quiz completion celebration
     * @param {object} results - Completion results
     */
    function onAssignmentComplete(results = {}) {
        const { score, total, isPerfect, pointsEarned } = results;

        if (window.SoundService) {
            if (isPerfect) {
                SoundService.playPerfect();
            } else {
                SoundService.playComplete();
            }
        }

        if (window.AnimationService) {
            if (isPerfect) {
                // Big celebration for perfect score
                AnimationService.fireCelebration();
                setTimeout(() => AnimationService.showTrophy(), 500);
            } else if (score / total >= 0.8) {
                // Good celebration for 80%+
                AnimationService.fireConfetti();
            }
        }

        // Show appropriate toast
        if (isPerfect) {
            showToast('PERFECT SCORE! Amazing work!', 'success', { duration: 5000 });
        } else if (score / total >= 0.8) {
            showToast(`Great job! ${score}/${total} correct!`, 'success', { duration: 4000 });
        } else {
            showToast(`Completed! ${score}/${total} correct. Keep practicing!`, 'info', { duration: 4000 });
        }

        // Show points after a delay
        if (pointsEarned) {
            setTimeout(() => {
                showPointsEarned(pointsEarned, 'assignment complete');
            }, 1500);
        }
    }

    /**
     * Show level up celebration
     * @param {number} newLevel - The new level reached
     */
    function onLevelUp(newLevel) {
        if (window.SoundService) {
            SoundService.playLevelUp();
        }

        if (window.AnimationService) {
            AnimationService.fireCelebration();
        }

        showToast(`Level Up! You're now level ${newLevel}!`, 'badge', { duration: 5000 });
    }

    /**
     * Add click feedback to a button
     * @param {Element} element - Button element
     */
    function addButtonFeedback(element) {
        if (!element) return;

        element.addEventListener('click', function (e) {
            if (window.SoundService) {
                SoundService.playClick();
            }

            if (window.AnimationService) {
                AnimationService.ripple(element, e);
            }
        });
    }

    /**
     * Initialize button feedback for all buttons with a specific class
     * @param {string} selector - CSS selector for buttons
     */
    function initButtonFeedback(selector = '.gamification-btn') {
        document.querySelectorAll(selector).forEach(button => {
            addButtonFeedback(button);
        });
    }

    /**
     * Show a success message
     * @param {string} message - Message to display
     */
    function success(message) {
        showToast(message, 'success');
    }

    /**
     * Show an error message
     * @param {string} message - Message to display
     */
    function error(message) {
        showToast(message, 'error');
    }

    /**
     * Show a warning message
     * @param {string} message - Message to display
     */
    function warning(message) {
        showToast(message, 'warning');
    }

    /**
     * Show an info message
     * @param {string} message - Message to display
     */
    function info(message) {
        showToast(message, 'info');
    }

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function () {
        // Auto-initialize buttons with gamification class
        initButtonFeedback();
    });

    // Public API
    return {
        // Toast notifications
        showToast,
        success,
        error,
        warning,
        info,

        // Answer feedback
        onCorrectAnswer,
        onWrongAnswer,

        // Progress notifications
        showPointsEarned,
        showStreak,
        showStreakMilestone,
        showBadgeEarned,
        onAssignmentComplete,
        onLevelUp,

        // Button effects
        addButtonFeedback,
        initButtonFeedback
    };
})();

// Make available globally
window.GamificationEffects = GamificationEffects;

// Convenience alias
window.GameFX = GamificationEffects;
