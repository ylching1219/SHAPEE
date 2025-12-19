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

//global search AJAX request
$(function () {
    var $input = $('#globalSearchInput');
    var $panel = $('#globalSearchPanel');
    var timer = null;

    function escapeHtml(text) {
        return $('<div>').text(text).html();
    }

    function highlightLabel(label, term) {
        term = term.trim();
        if (!term) return escapeHtml(label);

        var regex = new RegExp('(' + term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + ')', 'ig');
        return escapeHtml(label).replace(regex, '<span class="qs-highlight">$1</span>');
    }

    function render(results) {
        var term = $input.val() || '';
        var html = '';
    
        if (!results || !results.length) {
            //display no seach result
            html += '<div class="qs-empty">';
            html += '  <div class="qs-empty-icon"><i class="fas fa-search"></i></div>';
            html += '  <div class="qs-empty-title">No search results found</div>';
            html += '  <div class="qs-empty-sub">Try another keyword.</div>';
            html += '</div>';
    
            $panel.html(html).removeClass('d-none');
            return;
        }
    
        //display search result
        html += '<div class="qs-section">';
        html += '  <div class="qs-section-title">Go to</div>';
    
        results.forEach(function (x) {
            var label = x.group ? (x.group + ' \u203A ' + x.title) : x.title; // ›
            var highlighted = highlightLabel(label, term);
    
            html += '<a class="qs-item" href="' + x.url + '">';
            html += '  <div class="qs-item-text">';
            html += '    <div class="qs-item-label">' + highlighted + '</div>';
            html += '  </div>';
            html += '</a>';
        });
    
        html += '</div>';
    
        $panel.html(html).removeClass('d-none');
    }

    function doSearch() {
        var term = ($input.val() || '').trim();
        if (!term || term.length < 2) {
            $panel.addClass('d-none').empty();
            return;
        }

        $.get('/Search/Quick', { term: term }, function (res) {
            render(res.results);
        });
    }

    $input.on('keyup', function () {
        clearTimeout(timer);
        timer = setTimeout(doSearch, 250); // debounce
    });

    $('#globalSearchBtn').on('click', doSearch);

    //hide panel when clicking outside
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.navbar-search, #globalSearchPanel').length) {
            $panel.addClass('d-none');
        }
    });
});

//Notification Module
$(document).ready(function() {
    if ($('#notificationsDropdown').length === 0) {
        return;
    }

    let currentFilter = 'all';
    let pollInterval = 20000; // 20 seconds
    let pollTimer = null;

    //icon mapping for notification types
    const typeIcons = {
        'info': 'fas fa-info-circle',
        'success': 'fas fa-check-circle',
        'warning': 'fas fa-exclamation-triangle',
        'error': 'fas fa-times-circle'
    };

    //format relative time
    function formatRelativeTime(dateTime) {
        const now = new Date();
        const then = new Date(dateTime);
        const diffMs = now - then;
        const diffSec = Math.floor(diffMs / 1000);
        const diffMin = Math.floor(diffSec / 60);
        const diffHour = Math.floor(diffMin / 60);
        const diffDay = Math.floor(diffHour / 24);

        if (diffSec < 60) return 'just now';
        if (diffMin < 60) return `${diffMin}m ago`;
        if (diffHour < 24) return `${diffHour}h ago`;
        if (diffDay < 7) return `${diffDay}d ago`;
        return then.toLocaleDateString();
    }

    //render notification item HTML
    function renderNotificationItem(notification) {
        const iconClass = typeIcons[notification.type] || 'fas fa-bell';
        const iconBgClass = `icon ${notification.type}`;
        const itemClass = notification.isRead ? 'notification-item read' : 'notification-item unread';
        const title = notification.title.length > 60 
            ? notification.title.substring(0, 60) + '...' 
            : notification.title;
        const message = notification.message.length > 80 
            ? notification.message.substring(0, 80) + '...' 
            : notification.message;
        const relativeTime = formatRelativeTime(notification.createdAt);
        const clickUrl = notification.actionUrl || '#';
        const unreadDot = notification.isRead ? '' : '<span class="unread-dot"></span>';

        return `
            <a href="${clickUrl}" class="${itemClass}" data-notification-id="${notification.notificationId}" 
               onclick="markNotificationAsRead(${notification.notificationId}, event)">
                <div class="${iconBgClass}">
                    <i class="${iconClass}"></i>
                </div>
                <div class="content">
                    <div class="title-row">
                        <span class="title">${title}</span>
                        <span class="time">${relativeTime}</span>
                    </div>
                    <div class="message">${message}</div>
                </div>
                ${unreadDot}
            </a>
        `;
    }

        //load notifications
        function loadNotifications(filter = 'all') {
            const container = $('#notificationList');
            const footer = $('.notification-footer');
            if (container.length === 0) return;
    
            const url = '/Home/GetLatestNotifications?count=5&filter=' + filter;
    
            $.ajax({
                url: url,
                type: 'GET',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest'
                },
                success: function(response) {
                    if (response.notifications && response.notifications.length > 0) {
                        let html = '';
                        // Only show first 5 notifications
                        const displayNotifications = response.notifications.slice(0, 5);
                        displayNotifications.forEach(function(notif) {
                            html += renderNotificationItem(notif);
                        });
                        container.html(html);
    
                        // Update footer with "X more notifications" text
                        const displayedCount = displayNotifications.length;
                        const totalCount = response.totalCount || displayedCount;
                        const remainingCount = totalCount - displayedCount;
    
                        if (remainingCount > 0) {
                            footer.html(`
                                <span class="more-count">${remainingCount} more notification${remainingCount !== 1 ? 's' : ''}</span>
                                <a href="/Home/Notifications">View all notifications</a>
                            `);
                        } else {
                            footer.html('<a href="/Home/Notifications">View all notifications</a>');
                        }
                    } else {
                        container.html(`
                            <div class="text-center py-4">
                                <i class="fas fa-bell-slash fa-2x text-muted mb-2"></i>
                                <p class="small text-muted mb-0">No ${filter === 'all' ? '' : 'unread '}notifications</p>
                            </div>
                        `);
                        footer.html('<a href="/Home/Notifications">View all notifications</a>');
                    }
                },
                error: function(xhr, status, error) {
                    if (xhr.status !== 401) {
                        console.error('Error loading notifications:', {
                            status: xhr.status,
                            statusText: xhr.statusText,
                            error: error
                        });
                    }
                    container.html(`
                        <div class="text-center py-4">
                            <i class="fas fa-exclamation-circle fa-2x text-muted mb-2"></i>
                            <p class="small text-muted mb-0">Error loading notifications</p>
                        </div>
                    `);
                    footer.html('<a href="/Home/Notifications">View all notifications</a>');
                }
            });
        }

    //update unread count badge
    function updateUnreadCount() {
        const badge = $('#notificationBadge');
        if (badge.length === 0) return;

        $.ajax({
            url: '/Home/GetUnreadCount',
            type: 'GET',
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: function(response) {
                if (response && typeof response.count !== 'undefined') {
                    if (response.count > 0) {
                        badge.text(response.count > 99 ? '99+' : response.count).show();
                    } else {
                        badge.hide();
                    }
                }
            },
            error: function(xhr, status, error) {
                if (xhr.status !== 401) {
                    console.error('Error loading unread count:', {
                        status: xhr.status,
                        statusText: xhr.statusText,
                        error: error,
                        responseText: xhr.responseText
                    });
                }
                //hide badge on error (user might not be authenticated)
                badge.hide();
            }
        });
    }

    //mark notification as read (global function for onclick)
    window.markNotificationAsRead = function(notificationId, event) {
        //mark as read if not already read
        const $item = $(event.currentTarget);
        if ($item.hasClass('read')) {
            return; //already read, just navigate
        }

        $.ajax({
            url: '/Home/MarkAsRead',
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ notificationId: notificationId }),
            headers: {
                'X-Requested-With': 'XMLHttpRequest'
            },
            success: function() {
                $item.removeClass('unread').addClass('read');
                $item.find('.unread-dot').remove();
                updateUnreadCount();
                //reload current tab
                loadNotifications(currentFilter);
            },
            error: function() {
                console.error('Error marking notification as read');
            }
        });
    };

    //tab switching
    $(document).on('click', '.notification-tabs .tab', function(e) {
        e.preventDefault();
        e.stopPropagation();
        $('.notification-tabs .tab').removeClass('active');
        $(this).addClass('active');
        currentFilter = $(this).data('filter');
        loadNotifications(currentFilter);
    });

    //close dropdown button
    $(document).on('click', '.close-dropdown', function(e) {
        e.preventDefault();
        e.stopPropagation();
        $('#notificationsDropdown').dropdown('hide');
    });

    //initial load
    updateUnreadCount();
    loadNotifications('all');

    //start polling
    pollTimer = setInterval(function() {
        updateUnreadCount();
        loadNotifications(currentFilter);
    }, pollInterval);

    //cleanup on page unload
    $(window).on('beforeunload', function() {
        if (pollTimer) {
            clearInterval(pollTimer);
        }
    });
});


