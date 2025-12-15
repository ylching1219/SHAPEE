/**
 * SweetAlert2 util function
 * can be used for whole project to display alerts
 * 
 * @param {Object} options
 * @param {string} options.type - diff alert type: 'success', 'error', 'warning', 'info', 'question'
 * @param {string} options.title - title of the alert
 * @param {string} [options.text] - text/message of the alert
 * @param {string} [options.confirmButtonText] - text of the confirm button
 * @param {string} [options.confirmButtonColor] - color of the confirm button
 * @param {string} [options.cancelButtonText] - text of the cancel button
 * @param {boolean} [options.showCancelButton] - show cancel button
 * @param {Function} [options.onConfirm] - callback function when confirmed
 * @param {Function} [options.onCancel] - callback function when cancelled
 * @param {number} [options.timer] - auto close timer in milliseconds
 * @param {boolean} [options.timerProgressBar] - show progress bar for timer
 * @returns {Promise} SweetAlert2 promise
 */
function showSweetAlert(options) {
    const {
        type = 'info',
        title,
        text = '',
        confirmButtonText = 'OK',
        confirmButtonColor = '#4e73df',
        cancelButtonText = 'Cancel',
        showCancelButton = false,
        onConfirm = null,
        onCancel = null,
        timer = null,
        timerProgressBar = true
    } = options;

    const swalConfig = {
        icon: type,
        title: title,
        text: text,
        confirmButtonText: confirmButtonText,
        confirmButtonColor: confirmButtonColor,
        showCancelButton: showCancelButton,
        cancelButtonText: cancelButtonText,
        timer: timer,
        timerProgressBar: timerProgressBar
    };

    //if timer is set, remove cancel button
    if (timer) {
        swalConfig.showCancelButton = false;
    }

    return Swal.fire(swalConfig).then((result) => {
        if (result.isConfirmed && onConfirm) {
            onConfirm(result);
        } else if (result.isDismissed && onCancel) {
            onCancel(result);
        }
        return result;
    });
}

/**
 * quick helper functions for common alert types
 */
const SweetAlert = {
    success: (title, text, options = {}) => {
        return showSweetAlert({
            type: 'success',
            title: title,
            text: text,
            ...options
        });
    },

    error: (title, text, options = {}) => {
        return showSweetAlert({
            type: 'error',
            title: title,
            text: text,
            ...options
        });
    },

    warning: (title, text, options = {}) => {
        return showSweetAlert({
            type: 'warning',
            title: title,
            text: text,
            ...options
        });
    },

    info: (title, text, options = {}) => {
        return showSweetAlert({
            type: 'info',
            title: title,
            text: text,
            ...options
        });
    },

    confirm: (title, text, options = {}) => {
        return showSweetAlert({
            type: 'question',
            title: title,
            text: text,
            showCancelButton: true,
            confirmButtonText: options.confirmButtonText || 'Yes',
            cancelButtonText: options.cancelButtonText || 'No',
            ...options
        });
    }
};

// Initiate GET request (AJAX-supported)
$(document).on('click', '[data-get]', e => {
    e.preventDefault();
    const url = e.target.dataset.get;
    location = url || location;
});

// Initiate POST request (AJAX-supported)
$(document).on('click', '[data-post]', e => {
    e.preventDefault();
    const url = e.target.dataset.post;
    const f = $('<form>').appendTo(document.body)[0];
    f.method = 'post';
    f.action = url || location;
    f.submit();
});

// Auto-submit search form on keyup in search input
$("#searchInput").on("keyup", function () {
    $("#filterForm").submit();
});

$("#filter").on("change", function () {
    $("#filterForm").submit();
});


$("#filterForm").on("reset", function () {
    setTimeout(() => {
        $("#filterForm").submit();
    }, 0);
});

$("#genderFilter").on("change", function () {
    setTimeout(() => {
        $("#genderFilter").submit();
    }, 0);
});

$("#programFilter").on("change", function () {
    setTimeout(() => {
        $("#programFilter").submit();
    }, 0);
});

$("#statusFilter").on("change", function () {
    setTimeout(() => {
        $("#statusFilter").submit();
    }, 0);
});

$("#facultyFilter").on("change", function () {
    setTimeout(() => {
        $("#statusFilter").submit();
    }, 0);
});

$("#facultyFilter").on("change", function () {
    $("#filterForm").submit();
});




