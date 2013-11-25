﻿(function ($, document, apiClient) {

    var currentItem;
    
    function getDisplayTime(date) {
        
        try {

            date = parseISO8601Date(date, { toLocal: true });

        } catch (err) {
            return date;
        }

        date = date.toLocaleTimeString();

        date = date.replace('0:00', '0');

        return date;
    }

    function renderPrograms(page, result) {

        var html = '';

        var cssClass = "detailTable";

        html += '<div class="detailTableContainer"><table class="' + cssClass + '">';

        html += '<tr>';

        html += '<th>Date</th>';
        html += '<th>Start</th>';
        html += '<th>End</th>';
        html += '<th>Name</th>';
        html += '<th>Genre</th>';

        html += '</tr>';

        for (var i = 0, length = result.Items.length; i < length; i++) {

            var program = result.Items[i];

            html += '<tr>';

            var startDate = program.StartDate;

            try {

                startDate = parseISO8601Date(startDate, { toLocal: true });

            } catch (err) {

            }

            html += '<td>' + startDate.toLocaleDateString() + '</td>';
            
            html += '<td>' + getDisplayTime(program.StartDate) + '</td>';

            html += '<td>' + getDisplayTime(program.EndDate) + '</td>';

            html += '<td>' + (program.Name || '') + '</td>';
            html += '<td>' + program.Genres.join(' / ') + '</td>';

            html += '</tr>';
        }

        html += '</table></div>';

        $('#programList', page).html(html);
    }

    function loadPrograms(page) {

        ApiClient.getLiveTvPrograms({
            ChannelIds: currentItem.Id

        }).done(function (result) {

            renderPrograms(page, result);
        });
    }

    function reload(page) {

        Dashboard.showLoadingMsg();

        ApiClient.getLiveTvChannel(getParameterByName('id')).done(function (item) {

            currentItem = item;

            var name = item.Name;

            $('#itemImage', page).html(LibraryBrowser.getDetailImageHtml(item));

            Dashboard.setPageTitle(name);

            $('.itemName', page).html(name);

            if (ApiClient.isWebSocketOpen()) {

                var vals = [item.Type, item.Id, item.Name];

                vals.push('livetv');

                ApiClient.sendWebSocketMessage("Context", vals.join('|'));
            }

            if (MediaPlayer.canPlay(item)) {
                $('#playButtonContainer', page).show();
            } else {
                $('#playButtonContainer', page).hide();
            }

            Dashboard.getCurrentUser().done(function (user) {

                if (user.Configuration.IsAdministrator && item.LocationType !== "Offline") {
                    $('#editButtonContainer', page).show();
                } else {
                    $('#editButtonContainer', page).hide();
                }

            });

            loadPrograms(page);

            Dashboard.hideLoadingMsg();
        });
    }

    $(document).on('pageinit', "#liveTvChannelPage", function () {

        var page = this;

        $('#btnPlay', page).on('click', function () {
            var userdata = currentItem.UserData || {};
            LibraryBrowser.showPlayMenu(this, currentItem.Name, currentItem.Type, currentItem.MediaType, userdata.PlaybackPositionTicks);
        });

        $('#btnRemote', page).on('click', function () {

            RemoteControl.showMenuForItem({ item: currentItem, context: 'livetv' });
        });

        $('#btnEdit', page).on('click', function () {

            Dashboard.navigate("edititemmetadata.html?channelid=" + currentItem.Id);
        });

    }).on('pageshow', "#liveTvChannelPage", function () {

        var page = this;

        reload(page);

    }).on('pagehide', "#liveTvChannelPage", function () {

        currentItem = null;
    });

})(jQuery, document, ApiClient);