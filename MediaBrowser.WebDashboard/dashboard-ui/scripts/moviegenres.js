﻿(function ($, document) {

    var view = LibraryBrowser.getDefaultItemsView('Thumb', 'ThumbCard');

    // The base query options
    var query = {

        SortBy: "SortName",
        SortOrder: "Ascending",
        IncludeItemTypes: "Movie",
        Recursive: true,
        Fields: "DateCreated,SyncInfo,ItemCounts",
        StartIndex: 0
    };

    function getSavedQueryKey() {

        return 'moviegenres' + (query.ParentId || '');
    }

    function reloadItems(page) {

        Dashboard.showLoadingMsg();

        ApiClient.getGenres(Dashboard.getCurrentUserId(), query).done(function (result) {

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

            if (view == "Thumb") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    preferThumb: true,
                    context: 'movies',
                    showItemCounts: true,
                    centerText: true,
                    lazy: true
                });
            }
            else if (view == "ThumbCard") {

                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    preferThumb: true,
                    context: 'movies',
                    showItemCounts: true,
                    cardLayout: true,
                    showTitle: true,
                    lazy: true
                });
            }
            else if (view == "PosterCard") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "portrait",
                    context: 'movies',
                    showItemCounts: true,
                    lazy: true,
                    cardLayout: true,
                    showTitle: true
                });
            }
            else if (view == "Poster") {
                html = LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "portrait",
                    context: 'movies',
                    centerText: true,
                    showItemCounts: true,
                    lazy: true
                });
            }

            $('#items', page).html(html).lazyChildren();

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

        $('#selectPageSize', page).val(query.Limit).selectmenu('refresh');
        $('#selectView', page).val(view).selectmenu('refresh');
    }

    $(document).on('pageinitdepends', "#movieGenresPage", function () {

        var page = this;

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

        $('#selectPageSize', page).on('change', function () {
            query.Limit = parseInt(this.value);
            query.StartIndex = 0;
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

    }).on('pageshowready', "#movieGenresPage", function () {

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

        LibraryBrowser.getSavedViewSetting(viewkey).done(function (val) {

            if (val) {
                $('#selectView', page).val(val).selectmenu('refresh').trigger('change');
            } else {
                reloadItems(page);
            }
        });

        updateFilterControls(this);
    });

})(jQuery, document);