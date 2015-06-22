﻿(function ($, document) {

    var view = LibraryBrowser.getDefaultItemsView('List', 'List');
    var currentItem;

    // The base query options
    var query = {

        Fields: "PrimaryImageAspectRatio,SyncInfo",
        StartIndex: 0,
        ImageTypeLimit: 1,
        EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
    };

    function getSavedQueryKey() {

        return 'playlists' + (query.ParentId || '');
    }

    function getItemsFunction(itemsQuery) {

        itemsQuery = $.extend({}, itemsQuery);
        itemsQuery.SortBy = null;
        itemsQuery.SortOrder = null;

        return function (index, limit, fields) {

            itemsQuery.StartIndex = index;
            itemsQuery.Limit = limit;
            itemsQuery.Fields = fields;
            return ApiClient.getItems(Dashboard.getCurrentUserId(), itemsQuery);

        };

    }

    var _childrenItemsFunction = null;

    function reloadItems(page) {

        Dashboard.showLoadingMsg();

        query.ParentId = getParameterByName('id');
        query.UserId = Dashboard.getCurrentUserId();

        var promise1 = ApiClient.getJSON(ApiClient.getUrl('Playlists/' + query.ParentId + '/Items', query));
        var promise2 = Dashboard.getCurrentUser();
        var promise3 = ApiClient.getItem(Dashboard.getCurrentUserId(), query.ParentId);

        $.when(promise1, promise2, promise3).done(function (response1, response2, response3) {

            var result = response1[0];
            var user = response2[0];
            var item = response3[0];

            $('.playlistName', page).html(item.Name);

            _childrenItemsFunction = getItemsFunction(query);

            currentItem = item;

            if (MediaController.canPlay(item)) {
                $('.btnPlay', page).removeClass('hide');
            }
            else {
                $('.btnPlay', page).addClass('hide');
            }

            if (item.LocalTrailerCount && item.PlayAccess == 'Full') {
                $('.btnPlayTrailer', page).removeClass('hide');
            } else {
                $('.btnPlayTrailer', page).addClass('hide');
            }

            // Scroll back up so they can see the results from the beginning
            $(document).scrollTop(0);

            var html = '';

            $('.listTopPaging', page).html(LibraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit,
                totalRecordCount: result.TotalRecordCount,
                viewButton: true,
                showLimit: false
            })).trigger('create');

            updateFilterControls(page);

            if (result.TotalRecordCount) {

                if (view == "List") {

                    html = LibraryBrowser.getListViewHtml({
                        items: result.Items,
                        context: 'playlists',
                        sortBy: query.SortBy,
                        showIndex: false,
                        title: item.Name,
                        showRemoveFromPlaylist: true,
                        playFromHere: true,
                        defaultAction: 'playallfromhere',
                        smallIcon: true
                    });
                }

                $('.noItemsMessage', page).hide();

            } else {

                $('.noItemsMessage', page).show();
            }

            $('.itemsContainer', page).html(html).trigger('create').lazyChildren();

            $('.btnNextPage', page).on('click', function () {
                query.StartIndex += query.Limit;
                reloadItems(page);
            });

            $('.btnPreviousPage', page).on('click', function () {
                query.StartIndex -= query.Limit;
                reloadItems(page);
            });

            LibraryBrowser.saveQueryValues(getSavedQueryKey(), query);

            Dashboard.hideLoadingMsg();
        });
    }

    function removeFromPlaylist(page, ids) {

        ApiClient.ajax({

            url: ApiClient.getUrl('Playlists/' + currentItem.Id + '/Items', {
                EntryIds: ids.join(',')
            }),

            type: 'DELETE'

        }).done(function () {

            reloadItems(page);
        });

    }

    function updateFilterControls(page) {

        // Reset form values using the last used query
        $('.radioSortBy', page).each(function () {

            this.checked = (query.SortBy || '').toLowerCase() == this.getAttribute('data-sortby').toLowerCase();

        }).checkboxradio('refresh');

        $('.radioSortOrder', page).each(function () {

            this.checked = (query.SortOrder || '').toLowerCase() == this.getAttribute('data-sortorder').toLowerCase();

        }).checkboxradio('refresh');

        $('.chkStandardFilter', page).each(function () {

            var filters = "," + (query.Filters || "");
            var filterName = this.getAttribute('data-filter');

            this.checked = filters.indexOf(',' + filterName) != -1;

        }).checkboxradio('refresh');

        $('#selectView', page).val(view).selectmenu('refresh');

        $('#selectPageSize', page).val(query.Limit).selectmenu('refresh');
    }

    $(document).on('pageinitdepends', "#playlistEditorPage", function () {

        var page = this;

        $('#selectPageSize', page).on('change', function () {
            query.Limit = parseInt(this.value);
            query.StartIndex = 0;
            reloadItems(page);
        });

        $('.btnPlay', page).on('click', function () {
            var userdata = currentItem.UserData || {};

            var mediaType = currentItem.MediaType;

            if (currentItem.Type == "MusicArtist" || currentItem.Type == "MusicAlbum") {
                mediaType = "Audio";
            }

            LibraryBrowser.showPlayMenu(this, currentItem.Id, currentItem.Type, currentItem.IsFolder, mediaType, userdata.PlaybackPositionTicks);
        });

        $('.itemsContainer', page).on('needsrefresh', function () {

            reloadItems(page);

        }).on('removefromplaylist', function (e, playlistItemId) {

            removeFromPlaylist(page, [playlistItemId]);

        }).on('playallfromhere', function (e, index) {

            LibraryBrowser.playAllFromHere(_childrenItemsFunction, index);

        }).on('queueallfromhere', function (e, index) {

            LibraryBrowser.queueAllFromHere(_childrenItemsFunction, index);

        });

    }).on('pageshowready', "#playlistEditorPage", function () {

        var page = this;

        query.ParentId = LibraryMenu.getTopParentId();

        var limit = LibraryBrowser.getDefaultPageSize();

        // If the default page size has changed, the start index will have to be reset
        if (limit != query.Limit) {
            query.Limit = limit;
            query.StartIndex = 0;
        }

        var viewkey = getSavedQueryKey();

        LibraryBrowser.loadSavedQueryValues(viewkey, query);
        reloadItems(page);

        updateFilterControls(this);

    });

})(jQuery, document);