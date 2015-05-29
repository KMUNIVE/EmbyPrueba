﻿(function ($, document, window, clearTimeout, setTimeout) {

    var searchHintTimeout;

    function clearSearchHintTimeout() {

        if (searchHintTimeout) {

            clearTimeout(searchHintTimeout);
            searchHintTimeout = null;
        }
    }

    function getAdditionalTextLines(hint) {

        if (hint.Type == "Audio") {

            return [[hint.AlbumArtist, hint.Album].join(" - ")];

        }
        else if (hint.Type == "MusicAlbum") {

            return [hint.AlbumArtist];

        }
        else if (hint.Type == "MusicArtist") {

            return [Globalize.translate('LabelArtist')];

        }
        else if (hint.Type == "Movie") {

            return [Globalize.translate('LabelMovie')];

        }
        else if (hint.Type == "MusicVideo") {

            return [Globalize.translate('LabelMusicVideo')];
        }
        else if (hint.Type == "Episode") {

            return [Globalize.translate('LabelEpisode')];

        }
        else if (hint.Type == "Series") {

            return [Globalize.translate('LabelSeries')];
        }
        else if (hint.Type == "BoxSet") {

            return [Globalize.translate('LabelCollection')];
        }

        return [hint.Type];
    }

    function search() {

        var self = this;

        self.showSearchPanel = function () {

            var viewMenuSearch = $('.viewMenuSearch');

            viewMenuSearch.removeClass('hide');
            $('.headerSearchInput').focus();
        };
    }
    window.Search = new search();

    function renderSearchResultsInOverlay(elem, hints) {

        // Massage the objects to look like regular items
        hints = hints.map(function (i) {

            i.Id = i.ItemId;
            i.ImageTags = {};
            i.UserData = {};

            if (i.PrimaryImageTag) {
                i.ImageTags.Primary = i.PrimaryImageTag;
            }
            return i;
        });

        var html = LibraryBrowser.getPosterViewHtml({
            items: hints,
            shape: "square",
            lazy: true,
            overlayText: false,
            showTitle: true,
            coverImage: true,
            centerImage: true,
            textLines: getAdditionalTextLines,
            cardLayout: true
        });
        $('.itemsContainer', elem).html(html).lazyChildren();
    }

    function requestSearchHintsForOverlay(elem, searchTerm) {

        var currentTimeout = searchHintTimeout;

        ApiClient.getSearchHints({ userId: Dashboard.getCurrentUserId(), searchTerm: searchTerm, limit: 30 }).done(function (result) {

            if (currentTimeout != searchHintTimeout) {
                return;
            }

            renderSearchResultsInOverlay(elem, result.SearchHints);
        });
    }

    function updateSearchOverlay(elem, searchTerm) {

        if (!searchTerm) {

            $('.itemsContainer', elem).empty();
            clearSearchHintTimeout();
            return;
        }

        clearSearchHintTimeout();

        searchHintTimeout = setTimeout(function () {

            requestSearchHintsForOverlay(elem, searchTerm);

        }, 100);
    }

    function getSearchOverlay(createIfNeeded) {

        var elem = $('.searchResultsOverlay');

        if (createIfNeeded && !elem.length) {

            var html = '<div class="searchResultsOverlay ui-page-theme-b">';

            html += '<div class="searchResultsContainer"><div class="itemsContainer"></div></div></div>';

            elem = $(html).appendTo(document.body).hide().trigger('create');

            elem.createCardMenus();
        }

        return elem;
    }

    function onHeaderSearchChange(val) {

        if (val) {
            updateSearchOverlay(getSearchOverlay(true).fadeIn('fast'), val);
            $(document.body).addClass('bodyWithPopupOpen');

        } else {

            updateSearchOverlay(getSearchOverlay(false).fadeOut('fast'), val);
            $(document.body).removeClass('bodyWithPopupOpen');
        }
    }

    function bindSearchEvents() {

        $('.headerSearchInput').on("keyup", function (e) {

            // Down key
            if (e.keyCode == 40) {

                //var first = $('.card', panel)[0];

                //if (first) {
                //    first.focus();
                //}

                return false;

            } else {

                onHeaderSearchChange(this.value);
            }

        }).on("search", function (e) {

            if (!this.value) {

                onHeaderSearchChange('');
            }

        });

        $('.btnCloseSearch').on('click', closeSearchOverlay);

        $('.viewMenuSearchForm').on('submit', function () {

            return false;
        });
    }

    function closeSearchOverlay() {
        $('.headerSearchInput').val('');
        onHeaderSearchChange('');
        $('.viewMenuSearch').addClass('hide');
    }

    $(document).on('pagehide', ".libraryPage", function () {

        $('#txtSearch', this).val('');
        $('#searchHints', this).empty();

    }).on('pagecontainerbeforehide', closeSearchOverlay);

    $(document).on('headercreated', function () {

        bindSearchEvents();
    });

})(jQuery, document, window, clearTimeout, setTimeout);