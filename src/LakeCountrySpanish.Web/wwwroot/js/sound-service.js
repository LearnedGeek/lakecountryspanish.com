/**
 * Sound Service for Lake Country Spanish Gamification
 * Uses Howler.js for cross-browser audio playback
 */
const SoundService = (function () {
    'use strict';

    // Sound file paths
    const SOUND_PATH = '/sounds/';

    // Sound definitions with fallback to silent if file not found
    const sounds = {
        correct: null,
        wrong: null,
        complete: null,
        perfect: null,
        badge: null,
        points: null,
        streak: null,
        click: null,
        levelup: null
    };

    // User preferences
    let soundEnabled = true;
    let volume = 0.5;

    /**
     * Initialize the sound service
     */
    function init() {
        // Check user preference from localStorage
        const savedPref = localStorage.getItem('lcs_sound_enabled');
        if (savedPref !== null) {
            soundEnabled = savedPref === 'true';
        }

        const savedVolume = localStorage.getItem('lcs_sound_volume');
        if (savedVolume !== null) {
            volume = parseFloat(savedVolume);
        }

        // Preload sounds
        loadSounds();
    }

    /**
     * Load all sound files
     */
    function loadSounds() {
        const soundFiles = {
            correct: 'correct.mp3',
            wrong: 'wrong.mp3',
            complete: 'complete.mp3',
            perfect: 'perfect.mp3',
            badge: 'badge.mp3',
            points: 'points.mp3',
            streak: 'streak.mp3',
            click: 'click.mp3',
            levelup: 'levelup.mp3'
        };

        Object.keys(soundFiles).forEach(key => {
            try {
                sounds[key] = new Howl({
                    src: [SOUND_PATH + soundFiles[key]],
                    volume: volume,
                    preload: true,
                    onloaderror: function () {
                        console.log(`Sound file not found: ${soundFiles[key]} - using silent fallback`);
                        sounds[key] = null;
                    }
                });
            } catch (e) {
                console.log(`Failed to load sound: ${key}`);
                sounds[key] = null;
            }
        });
    }

    /**
     * Play a sound effect
     * @param {string} soundName - Name of the sound to play
     * @param {object} options - Optional settings (volume, rate)
     */
    function play(soundName, options = {}) {
        if (!soundEnabled) return;

        // Check for reduced motion preference (also applies to sound for some users)
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
            return;
        }

        const sound = sounds[soundName];
        if (sound) {
            const playVolume = options.volume !== undefined ? options.volume : volume;
            sound.volume(playVolume);

            if (options.rate) {
                sound.rate(options.rate);
            }

            sound.play();
        }
    }

    /**
     * Play correct answer sound
     */
    function playCorrect() {
        play('correct');
    }

    /**
     * Play wrong answer sound
     */
    function playWrong() {
        play('wrong');
    }

    /**
     * Play completion sound (assignment/quiz finished)
     */
    function playComplete() {
        play('complete');
    }

    /**
     * Play perfect score sound
     */
    function playPerfect() {
        play('perfect');
    }

    /**
     * Play badge earned sound
     */
    function playBadge() {
        play('badge');
    }

    /**
     * Play points earned sound
     */
    function playPoints() {
        play('points', { volume: volume * 0.7 }); // Slightly quieter for frequent sound
    }

    /**
     * Play streak sound
     */
    function playStreak() {
        play('streak');
    }

    /**
     * Play button click sound
     */
    function playClick() {
        play('click', { volume: volume * 0.5 }); // Subtle click
    }

    /**
     * Play level up sound
     */
    function playLevelUp() {
        play('levelup');
    }

    /**
     * Enable or disable sounds
     * @param {boolean} enabled
     */
    function setEnabled(enabled) {
        soundEnabled = enabled;
        localStorage.setItem('lcs_sound_enabled', enabled.toString());
    }

    /**
     * Check if sounds are enabled
     * @returns {boolean}
     */
    function isEnabled() {
        return soundEnabled;
    }

    /**
     * Set the volume level
     * @param {number} level - Volume from 0 to 1
     */
    function setVolume(level) {
        volume = Math.max(0, Math.min(1, level));
        localStorage.setItem('lcs_sound_volume', volume.toString());

        // Update all loaded sounds
        Object.values(sounds).forEach(sound => {
            if (sound) {
                sound.volume(volume);
            }
        });
    }

    /**
     * Get current volume level
     * @returns {number}
     */
    function getVolume() {
        return volume;
    }

    /**
     * Toggle sound on/off
     * @returns {boolean} New enabled state
     */
    function toggle() {
        setEnabled(!soundEnabled);
        return soundEnabled;
    }

    // Initialize on load
    if (typeof Howl !== 'undefined') {
        init();
    } else {
        // Wait for Howler to load
        window.addEventListener('load', function () {
            if (typeof Howl !== 'undefined') {
                init();
            }
        });
    }

    // Public API
    return {
        play,
        playCorrect,
        playWrong,
        playComplete,
        playPerfect,
        playBadge,
        playPoints,
        playStreak,
        playClick,
        playLevelUp,
        setEnabled,
        isEnabled,
        setVolume,
        getVolume,
        toggle
    };
})();

// Make available globally
window.SoundService = SoundService;
