﻿(function ($, document, Dashboard, LibraryBrowser) {

    function notifications() {

        var self = this;

        self.getNotificationsSummaryPromise = null;

        self.total = 0;

        self.getNotificationsSummary = function () {

            var apiClient = window.ApiClient;

            if (!apiClient) {
                return;
            }

            self.getNotificationsSummaryPromise = self.getNotificationsSummaryPromise || apiClient.getNotificationSummary(Dashboard.getCurrentUserId());

            return self.getNotificationsSummaryPromise;
        };

        self.updateNotificationCount = function () {

            if (!Dashboard.getCurrentUserId()) {
                return;
            }

            var promise = self.getNotificationsSummary();

            if (!promise) {
                return;
            }

            promise.done(function (summary) {

                var item = $('.btnNotificationsInner').removeClass('levelNormal').removeClass('levelWarning').removeClass('levelError').html(summary.UnreadCount);

                if (summary.UnreadCount) {
                    item.addClass('level' + summary.MaxUnreadNotificationLevel);
                }
            });
        };

        self.showNotificationsFlyout = function () {

            Dashboard.getCurrentUser().done(function (user) {
                var html = '<div data-role="panel" data-position="right" data-display="overlay" class="notificationsFlyout" data-position-fixed="true" data-theme="a">';

                html += '<h1 style="margin: .25em 0;">';
                html += '<span style="vertical-align:middle;">' + Globalize.translate('HeaderNotifications') + '</span>';

                if (user.Policy.IsAdministrator) {
                    html += '<a data-role="button" data-inline="true" data-icon="arrow-r" href="notificationlist.html" data-iconpos="notext" style="vertical-align:middle;margin-left:.5em;">' + Globalize.translate('ButtonViewNotifications') + '</a>';
                }

                html += '</h1>';

                html += '<div>';

                html += '<div class="notificationsFlyoutlist">Loading...';

                html += '</div>';

                html += '</div>';

                html += '</div>';

                $(document.body).append(html);

                $('.notificationsFlyout').panel({}).trigger('create').panel("open").on("panelclose", function () {

                    $(this).off("panelclose").remove();

                });

                self.isFlyout = true;

                var startIndex = 0;
                var limit = 5;
                var elem = $('.notificationsFlyoutlist');

                refreshNotifications(startIndex, limit, elem, null, false).done(function () {

                    self.markNotificationsRead([]);
                });
            });
        };

        self.markNotificationsRead = function (ids, callback) {

            ApiClient.markNotificationsRead(Dashboard.getCurrentUserId(), ids, true).done(function () {

                self.getNotificationsSummaryPromise = null;

                self.updateNotificationCount();

                if (callback) {
                    callback();
                }

            });

        };

        self.showNotificationsList = function (startIndex, limit, elem, btn) {

            refreshNotifications(startIndex, limit, elem, btn, true);

        };
    }

    function refreshNotifications(startIndex, limit, elem, btn, showPaging) {

        var apiClient = window.ApiClient;

        if (apiClient) {
            return apiClient.getNotifications(Dashboard.getCurrentUserId(), { StartIndex: startIndex, Limit: limit }).done(function (result) {

                listUnreadNotifications(result.Notifications, result.TotalRecordCount, startIndex, limit, elem, btn, showPaging);

            });
        }
    }

    function listUnreadNotifications(list, totalRecordCount, startIndex, limit, elem, btn, showPaging) {

        if (!totalRecordCount) {
            elem.html('<p style="padding:.5em 1em;">' + Globalize.translate('LabelNoUnreadNotifications') + '</p>');

            if (btn) {
                btn.hide();
            }
            return;
        }

        Notifications.total = totalRecordCount;

        if (btn) {
            if (list.filter(function (n) {

                return !n.IsRead;

            }).length) {
                btn.show();
            } else {
                btn.hide();
            }
        }

        var html = '';

        if (totalRecordCount > limit && showPaging === true) {

            var query = { StartIndex: startIndex, Limit: limit };

            html += LibraryBrowser.getPagingHtml(query, totalRecordCount, false, limit, false);
        }

        for (var i = 0, length = list.length; i < length; i++) {

            var notification = list[i];

            html += getNotificationHtml(notification);

        }

        elem.html(html).trigger('create');
    }

    function getNotificationHtml(notification) {

        var html = '';

        var cssClass = notification.IsRead ? "flyoutNotification" : "flyoutNotification unreadFlyoutNotification";

        html += '<div data-notificationid="' + notification.Id + '" class="' + cssClass + '">';

        html += '<div class="notificationImage">';
        html += getImageHtml(notification);
        html += '</div>';

        html += '<div class="notificationContent">';

        html += '<p style="font-size:16px;margin: .5em 0 .5em;" class="notificationName">';
        if (notification.Url) {
            html += '<a href="' + notification.Url + '" target="_blank" style="text-decoration:none;">' + notification.Name + '</a>';
        } else {
            html += notification.Name;
        }
        html += '</p>';

        html += '<p class="notificationTime" style="margin: .5em 0;">' + humane_date(notification.Date) + '</p>';

        if (notification.Description) {
            html += '<p style="margin: .5em 0;max-height:100px;overflow:hidden;text-overflow:ellipsis;">' + notification.Description + '</p>';
        }

        html += '</div>';

        html += '</div>';

        return html;
    }

    function getImageHtml(notification) {

        if (notification.Level == "Error") {

            return '<div class="imgNotification imgNotificationError"><div class="imgNotificationInner imgNotificationIcon"></div></div>';

        }
        if (notification.Level == "Warning") {

            return '<div class="imgNotification imgNotificationWarning"><div class="imgNotificationInner imgNotificationIcon"></div></div>';

        }

        return '<div class="imgNotification imgNotificationNormal"><div class="imgNotificationInner imgNotificationIcon"></div></div>';

    }

    window.Notifications = new notifications();

    $(document).on('headercreated', function (e) {

        if (window.ApiClient) {
            $('<button class="headerButton headerButtonRight btnNotifications" data-role="none" type="button" title="Notifications"><div class="btnNotificationsInner">0</div></button>').insertAfter($('.headerSearchButton')).on('click', Notifications.showNotificationsFlyout);

            Notifications.updateNotificationCount();
        }
    });

    function onWebSocketMessage(e, msg) {
        if (msg.MessageType === "NotificationUpdated" || msg.MessageType === "NotificationAdded" || msg.MessageType === "NotificationsMarkedRead") {

            Notifications.getNotificationsSummaryPromise = null;

            Notifications.updateNotificationCount();
        }
    }

    function initializeApiClient(apiClient) {
        $(apiClient).off("websocketmessage", onWebSocketMessage).on("websocketmessage", onWebSocketMessage);
    }

    Dashboard.ready(function () {

        if (window.ApiClient) {
            initializeApiClient(window.ApiClient);
        }

        $(ConnectionManager).on('apiclientcreated', function (e, apiClient) {
            initializeApiClient(apiClient);
        });
    });

})(jQuery, document, Dashboard, LibraryBrowser);