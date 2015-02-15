﻿(function ($, document) {

    var view = LibraryBrowser.getDefaultItemsView('Poster', 'Poster');

    // The base query options
    var query = {

        SortBy: "SortName",
        SortOrder: "Ascending",
        IncludeItemTypes: "Movie",
        Recursive: true,
        Fields: "PrimaryImageAspectRatio,SortName,MediaSourceCount,IsUnidentified,SyncInfo",
        StartIndex: 0,
        ImageTypeLimit: 1,
        EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
    };

    function getSavedQueryKey() {

        return 'movies' + (query.ParentId || '');
    }

    function reloadItems(page) {

        Dashboard.showLoadingMsg();

        var userId = Dashboard.getCurrentUserId();

        ApiClient.getItems(userId, query).done(function (result) {

            // Scroll back up so they can see the results from the beginning
            $(document).scrollTop(0);

            var html = '';

            var pagingHtml = LibraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit,
                totalRecordCount: result.TotalRecordCount,
                viewButton: true,
                showLimit: false,
                addSelectionButton: true

            });

            $('.listTopPaging', page).html(pagingHtml).trigger('create');

            updateFilterControls(page);
            var trigger = false;

            if (view == "Thumb") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    preferThumb: true,
                    context: 'movies',
                    lazy: true,
                    overlayText: true,
                    showTitle: false
                });
            }
            else if (view == "ThumbCard") {

                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    preferThumb: true,
                    context: 'movies',
                    lazy: true,
                    showTitle: true,
                    cardLayout: true,
                    showYear: true
                });
            }
            else if (view == "Banner") {

                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "banner",
                    preferBanner: true,
                    context: 'movies',
                    lazy: true
                });
            }
            else if (view == "List") {

                html = LibraryBrowser.getListViewHtml({
                    items: result.Items,
                    context: 'movies',
                    sortBy: query.SortBy
                });
                trigger = true;
            }
            else if (view == "Poster") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "portrait",
                    context: 'movies',
                    showTitle: false,
                    centerText: true,
                    lazy: true,
                    overlayText: true
                });
            }
            else if (view == "PosterCard") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "portrait",
                    context: 'movies',
                    showTitle: true,
                    showYear: true,
                    lazy: true,
                    cardLayout: true
                });
            }
            else if (view == "Timeline") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "portrait",
                    context: 'movies',
                    showTitle: true,
                    timeline: true,
                    centerText: true,
                    lazy: true
                });
            }

            var elem = $('.itemsContainer', page).html(html).lazyChildren();

            if (trigger) {
                $(elem).trigger('create');
            }

            $(pagingHtml).appendTo(elem).trigger('create');

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

        $('.chkVideoTypeFilter', page).each(function () {

            var filters = "," + (query.VideoTypes || "");
            var filterName = this.getAttribute('data-filter');

            this.checked = filters.indexOf(',' + filterName) != -1;

        }).checkboxradio('refresh');

        $('#selectView', page).val(view).selectmenu('refresh');

        $('#chk3D', page).checked(query.Is3D == true).checkboxradio('refresh');
        $('#chkHD', page).checked(query.IsHD == true).checkboxradio('refresh');
        $('#chkSD', page).checked(query.IsHD == false).checkboxradio('refresh');

        $('#chkSubtitle', page).checked(query.HasSubtitles == true).checkboxradio('refresh');
        $('#chkTrailer', page).checked(query.HasTrailer == true).checkboxradio('refresh');
        $('#chkSpecialFeature', page).checked(query.HasSpecialFeature == true).checkboxradio('refresh');
        $('#chkThemeSong', page).checked(query.HasThemeSong == true).checkboxradio('refresh');
        $('#chkThemeVideo', page).checked(query.HasThemeVideo == true).checkboxradio('refresh');

        $('.alphabetPicker', page).alphaValue(query.NameStartsWithOrGreater);
        $('#selectPageSize', page).val(query.Limit).selectmenu('refresh');
    }

    var filtersLoaded;
    function reloadFiltersIfNeeded(page) {

        if (!filtersLoaded) {

            filtersLoaded = true;

            QueryFilters.loadFilters(page, Dashboard.getCurrentUserId(), query, function () {

                reloadItems(page);
            });
        }
    }

    $(document).on('pageinit', "#moviesPage", function () {

        var page = this;

        $('.viewPanel', page).on('panelopen', function () {

            reloadFiltersIfNeeded(page);
        });

        $('.radioSortBy', this).on('click', function () {
            query.StartIndex = 0;
            query.SortBy = this.getAttribute('data-sortby');
            reloadItems(page);
        });

        $('.radioSortOrder', this).on('click', function () {
            query.StartIndex = 0;
            query.SortOrder = this.getAttribute('data-sortorder');
            reloadItems(page);
        });

        $('.chkStandardFilter', this).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.Filters || "";

            filters = (',' + filters).replace(',' + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + ',' + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.Filters = filters;

            reloadItems(page);
        });

        $('#selectView', this).on('change', function () {

            view = this.value;

            if (view == "Timeline") {

                query.SortBy = "PremiereDate";
                query.SortOrder = "Descending";
                query.StartIndex = 0;
                $('#radioPremiereDate', page)[0].click();

            } else {
                reloadItems(page);
            }

            LibraryBrowser.saveViewSetting(getSavedQueryKey(), view);
        });

        $('.chkVideoTypeFilter', this).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.VideoTypes || "";

            filters = (',' + filters).replace(',' + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + ',' + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.VideoTypes = filters;

            reloadItems(page);
        });

        $('#chk3D', this).on('change', function () {

            query.StartIndex = 0;
            query.Is3D = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkHD', this).on('change', function () {

            query.StartIndex = 0;
            query.IsHD = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkSD', this).on('change', function () {

            query.StartIndex = 0;
            query.IsHD = this.checked ? false : null;

            reloadItems(page);
        });

        $('#chkSubtitle', this).on('change', function () {

            query.StartIndex = 0;
            query.HasSubtitles = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkTrailer', this).on('change', function () {

            query.StartIndex = 0;
            query.HasTrailer = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkSpecialFeature', this).on('change', function () {

            query.StartIndex = 0;
            query.HasSpecialFeature = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkThemeSong', this).on('change', function () {

            query.StartIndex = 0;
            query.HasThemeSong = this.checked ? true : null;

            reloadItems(page);
        });

        $('#chkThemeVideo', this).on('change', function () {

            query.StartIndex = 0;
            query.HasThemeVideo = this.checked ? true : null;

            reloadItems(page);
        });

        $('.alphabetPicker', this).on('alphaselect', function (e, character) {

            query.NameStartsWithOrGreater = character;
            query.StartIndex = 0;

            reloadItems(page);

        }).on('alphaclear', function (e) {

            query.NameStartsWithOrGreater = '';

            reloadItems(page);
        });

        $('#radioBasicFilters', this).on('change', function () {

            if (this.checked) {
                $('.basicFilters', page).show();
                $('.advancedFilters', page).hide();
            } else {
                $('.basicFilters', page).hide();
            }
        });

        $('#radioAdvancedFilters', this).on('change', function () {

            if (this.checked) {
                $('.advancedFilters', page).show();
                $('.basicFilters', page).hide();
            } else {
                $('.advancedFilters', page).hide();
            }
        });

        $('.itemsContainer', page).on('needsrefresh', function () {

            reloadItems(page);

        });

        $('#selectPageSize', page).on('change', function () {
            query.Limit = parseInt(this.value);
            query.StartIndex = 0;
            reloadItems(page);
        });

    }).on('pagebeforeshow', "#moviesPage", function () {

        query.ParentId = LibraryMenu.getTopParentId();

        var page = this;
        var limit = LibraryBrowser.getDefaultPageSize();

        // If the default page size has changed, the start index will have to be reset
        if (limit != query.Limit) {
            query.Limit = limit;
            query.StartIndex = 0;
        }

        var viewkey = getSavedQueryKey();

        LibraryBrowser.loadSavedQueryValues(viewkey, query);

        QueryFilters.onPageShow(page, query);

        LibraryBrowser.getSavedViewSetting(viewkey).done(function (val) {

            if (val) {
                $('#selectView', page).val(val).selectmenu('refresh').trigger('change');
            } else {
                reloadItems(page);
            }
        });

    }).on('pageshow', "#moviesPage", function () {

        var page = this;

        updateFilterControls(page);

        filtersLoaded = false;
    });

})(jQuery, document);

(function (window) {

    function renderOptions(page, selector, cssClass, items) {

        var elem;

        if (items.length) {

            elem = $(selector, page).show();

        } else {
            elem = $(selector, page).hide();
        }

        var html = '';

        //  style="margin: -.2em -.8em;"
        html += '<div data-role="controlgroup">';

        var index = 0;
        var idPrefix = 'chk' + selector.substring(1);

        html += items.map(function (filter) {

            var itemHtml = '';

            var id = idPrefix + index;
            itemHtml += '<label for="' + id + '">' + filter + '</label>';
            itemHtml += '<input id="' + id + '" type="checkbox" data-filter="' + filter + '" data-mini="true" class="' + cssClass + '" />';

            index++;

            return itemHtml;

        }).join('');

        html += '</div>';

        $('.filterOptions', elem).html(html).trigger('create');
    }

    function renderFilters(page, result) {

        // If there's a huge number of these they will be really show to render
        if (result.Tags) {
            result.Tags.length = Math.min(result.Tags.length, 50);
        }

        renderOptions(page, '.genreFilters', 'chkGenreFilter', result.Genres);
        renderOptions(page, '.officialRatingFilters', 'chkOfficialRatingFilter', result.OfficialRatings);
        renderOptions(page, '.tagFilters', 'chkTagFilter', result.Tags);
        renderOptions(page, '.yearFilters', 'chkYearFilter', result.Years);

    }

    function onFiltersLoaded(page, query, reloadItemsFn) {

        $('.chkGenreFilter', page).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.Genres || "";
            var delimiter = '|';

            filters = (delimiter + filters).replace(delimiter + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + delimiter + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.Genres = filters;

            reloadItemsFn();
        });
        $('.chkTagFilter', page).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.Tags || "";
            var delimiter = '|';

            filters = (delimiter + filters).replace(delimiter + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + delimiter + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.Tags = filters;

            reloadItemsFn();
        });
        $('.chkYearFilter', page).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.Years || "";
            var delimiter = ',';

            filters = (delimiter + filters).replace(delimiter + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + delimiter + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.Years = filters;

            reloadItemsFn();
        });
        $('.chkOfficialRatingFilter', page).on('change', function () {

            var filterName = this.getAttribute('data-filter');
            var filters = query.OfficialRatings || "";
            var delimiter = '|';

            filters = (delimiter + filters).replace(delimiter + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + delimiter + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.OfficialRatings = filters;

            reloadItemsFn();
        });
    }

    function loadFilters(page, userId, itemQuery, reloadItemsFn) {

        return ApiClient.getJSON(ApiClient.getUrl('Items/Filters', {

            UserId: userId,
            ParentId: itemQuery.ParentId,
            IncludeItemTypes: itemQuery.IncludeItemTypes


        })).done(function (result) {

            renderFilters(page, result);

            onFiltersLoaded(page, itemQuery, reloadItemsFn);
        });

    }

    function onPageShow(page, query) {
        query.Genres = null;
        query.Years = null;
        query.OfficialRatings = null;
        query.Tags = null;
    }

    window.QueryFilters = {
        loadFilters: loadFilters,
        onPageShow: onPageShow
    };

})(window);