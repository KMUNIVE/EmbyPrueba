﻿(function ($, document) {

    function getView() {

        return 'Thumb';
    }

    function reload(page) {

        Dashboard.showLoadingMsg();

        loadNextUp(page, 'home-nextup');
    }

    function loadNextUp(page) {

        var limit = AppInfo.hasLowImageBandwidth ?
         16 :
         24;

        var query = {

            Limit: limit,
            Fields: "PrimaryImageAspectRatio,SeriesInfo,DateCreated,SyncInfo",
            UserId: Dashboard.getCurrentUserId(),
            ExcludeLocationTypes: "Virtual",
            ImageTypeLimit: 1,
            EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
        };

        ApiClient.getNextUpEpisodes(query).done(function (result) {

            if (result.Items.length) {
                page.querySelector('.noNextUpItems').classList.add('hide');
            } else {
                page.querySelector('.noNextUpItems').classList.remove('hide');
            }
            var view = getView();
            var html = '';

            if (view == 'ThumbCard') {

                html += LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    showTitle: true,
                    preferThumb: true,
                    showParentTitle: true,
                    lazy: true,
                    cardLayout: true,
                    showDetailsMenu: true,
                    context: 'home-nextup'
                });

            } else if (view == 'Thumb') {

                html += LibraryBrowser.getPosterViewHtml({
                    items: result.Items,
                    shape: "backdrop",
                    showTitle: true,
                    showParentTitle: true,
                    overlayText: false,
                    lazy: true,
                    preferThumb: true,
                    showDetailsMenu: true,
                    centerText: true,
                    overlayPlayButton: AppInfo.enableAppLayouts,
                    context: 'home-nextup'
                });
            }

            var elem = page.querySelector('#nextUpItems');
            elem.innerHTML = html;
            ImageLoader.lazyChildren(elem);
            Dashboard.hideLoadingMsg();

            LibraryBrowser.setLastRefreshed(page);
        });
    }

    window.HomePage.renderNextUp = function (page, tabContent) {
        if (LibraryBrowser.needsRefresh(tabContent)) {
            reload(tabContent);
        }
    };

})(jQuery, document);