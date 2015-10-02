﻿(function ($, document) {

    var pageSizeKey = 'people';
    var data = {};

    function getQuery() {

        var key = getSavedQueryKey();
        var pageData = data[key];

        if (!pageData) {
            pageData = data[key] = {
                query: {
                    SortBy: "SortName",
                    SortOrder: "Ascending",
                    IncludeItemTypes: "Series,Episode",
                    Recursive: true,
                    Fields: "DateCreated,ItemCounts",
                    PersonTypes: "",
                    StartIndex: 0,
                    Limit: 100
                }
            };

            pageData.query.ParentId = LibraryMenu.getTopParentId();
            LibraryBrowser.loadSavedQueryValues(key, pageData.query);
        }
        return pageData.query;
    }

    function getSavedQueryKey() {

        return getWindowUrl() + 'people';
    }

    function reloadItems(page, viewPanel) {

        Dashboard.showLoadingMsg();

        var query = getQuery();
        ApiClient.getPeople(Dashboard.getCurrentUserId(), query).done(function (result) {

            // Scroll back up so they can see the results from the beginning
            window.scrollTo(0, 0);

            var html = '';
            var pagingHtml = LibraryBrowser.getQueryPagingHtml({
                startIndex: query.StartIndex,
                limit: query.Limit,
                totalRecordCount: result.TotalRecordCount,
                viewButton: true,
                viewIcon: 'filter-list',
                showLimit: false,
                updatePageSizeSetting: false,
                pageSizeKey: pageSizeKey,
                viewPanelClass: 'peopleViewPanel'
            });

            page.querySelector('.listTopPaging').innerHTML = pagingHtml;

            updateFilterControls(page, viewPanel);

            html = LibraryBrowser.getPosterViewHtml({
                items: result.Items,
                shape: "portrait",
                context: 'tv',
                showTitle: true,
                showItemCounts: true,
                coverImage: true,
                lazy: true
            });

            var elem = page.querySelector('.itemsContainer');
            elem.innerHTML = html + pagingHtml;
            ImageLoader.lazyChildren(elem);

            $('.btnNextPage', page).on('click', function () {
                query.StartIndex += query.Limit;
                reloadItems(page, viewPanel);
            });

            $('.btnPreviousPage', page).on('click', function () {
                query.StartIndex -= query.Limit;
                reloadItems(page, viewPanel);
            });

            LibraryBrowser.saveQueryValues(getSavedQueryKey(), query);

            Dashboard.hideLoadingMsg();
        });
    }

    function updateFilterControls(tabContent, viewPanel) {

        var query = getQuery();
        $('.chkPersonTypeFilter', viewPanel).each(function () {

            var filters = "," + (query.PersonTypes || "");
            var filterName = this.getAttribute('data-filter');

            this.checked = filters.indexOf(',' + filterName) != -1;

        });

        $('.alphabetPicker', tabContent).alphaValue(query.NameStartsWithOrGreater);
    }

    function initPage(tabContent, viewPanel) {

        $('.chkStandardFilter', viewPanel).on('change', function () {

            var query = getQuery();
            var filterName = this.getAttribute('data-filter');
            var filters = query.Filters || "";

            filters = (',' + filters).replace(',' + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + ',' + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.Filters = filters;

            reloadItems(tabContent, viewPanel);
        });

        $('.chkPersonTypeFilter', viewPanel).on('change', function () {

            var query = getQuery();
            var filterName = this.getAttribute('data-filter');
            var filters = query.PersonTypes || "";

            filters = (',' + filters).replace(',' + filterName, '').substring(1);

            if (this.checked) {
                filters = filters ? (filters + ',' + filterName) : filterName;
            }

            query.StartIndex = 0;
            query.PersonTypes = filters;

            reloadItems(tabContent, viewPanel);
        });

        $('.alphabetPicker', tabContent).on('alphaselect', function (e, character) {

            var query = getQuery();
            query.NameStartsWithOrGreater = character;
            query.StartIndex = 0;

            reloadItems(tabContent, viewPanel);

        }).on('alphaclear', function (e) {

            var query = getQuery();
            query.NameStartsWithOrGreater = '';

            reloadItems(tabContent, viewPanel);
        });
    }

    window.TvPage.initPeopleTab = function (page, tabContent) {

        var viewPanel = page.querySelector('.peopleViewPanel');
        initPage(tabContent, viewPanel);
    };

    window.TvPage.renderPeopleTab = function (page, tabContent) {

        if (LibraryBrowser.needsRefresh(tabContent)) {
            var viewPanel = page.querySelector('.peopleViewPanel');
            reloadItems(tabContent, viewPanel);
            updateFilterControls(tabContent, viewPanel);
        }
    };

})(jQuery, document);