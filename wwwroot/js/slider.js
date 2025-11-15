/**
 * Generic Slider Function
 * Can be used for any slider with slides and indicators
 * 
 * @param {Object} options - Configuration options
 * @param {string} options.slideSelector - CSS selector for slides (e.g., '.content-slide' or '.feature-slide')
 * @param {string} options.indicatorSelector - CSS selector for indicators (e.g., '.indicator' or '.feature-indicator')
 * @param {string} options.sliderSelector - CSS selector for the slider container (e.g., '.content-slider' or '.features-slider')
 * @param {number} options.interval - Time in milliseconds between slides (default: 4000)
 */
function initSlider(options) {
    const {
        slideSelector,
        indicatorSelector,
        sliderSelector,
        interval = 4000
    } = options;

    const slides = document.querySelectorAll(slideSelector);
    const indicators = document.querySelectorAll(indicatorSelector);
    const slider = document.querySelector(sliderSelector);

    if (!slides.length || !slider) {
        return; 
    }

    let currentSlide = 0;
    let intervalId = null;

    function showSlide(index) {
        slides.forEach(slide => slide.classList.remove('active'));
        if (indicators.length > 0) {
            indicators.forEach(indicator => indicator.classList.remove('active'));
        }

        if (slides[index]) {
            slides[index].classList.add('active');
        }
        if (indicators[index]) {
            indicators[index].classList.add('active');
        }
    }

    function nextSlide() {
        currentSlide = (currentSlide + 1) % slides.length;
        showSlide(currentSlide);
    }

    function startSlider() {
        if (intervalId) {
            clearInterval(intervalId);
        }
        intervalId = setInterval(nextSlide, interval);
    }

    function stopSlider() {
        if (intervalId) {
            clearInterval(intervalId);
            intervalId = null;
        }
    }

    //initialize first slide
    showSlide(0);

    //start auto-rotation
    startSlider();

    indicators.forEach((indicator, index) => {
        indicator.addEventListener('click', () => {
            currentSlide = index;
            showSlide(currentSlide);
            startSlider(); 
        });
    });

    //pause on hover
    slider.addEventListener('mouseenter', stopSlider);
    slider.addEventListener('mouseleave', startSlider);

    //return control functions in case needed
    return {
        next: nextSlide,
        show: showSlide,
        start: startSlider,
        stop: stopSlider,
        goTo: (index) => {
            currentSlide = index;
            showSlide(currentSlide);
            startSlider();
        }
    };
}

//initialize tje slider when ready
document.addEventListener('DOMContentLoaded', function () {
    //landing page slider
    const landingSlider = document.querySelector('.content-slider');
    if (landingSlider) {
        initSlider({
            slideSelector: '.content-slide',
            indicatorSelector: '.indicator',
            sliderSelector: '.content-slider',
            interval: 3000
        });
    }

    //login page slider
    const loginSlider = document.querySelector('.features-slider');
    if (loginSlider) {
        initSlider({
            slideSelector: '.feature-slide',
            indicatorSelector: '.feature-indicator',
            sliderSelector: '.features-slider',
            interval: 3000
        });
    }
});
