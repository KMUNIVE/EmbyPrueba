﻿(function ($, document) {

    $(document).on('pageshowready', "#tvUpcomingPage", function () {

        var page = this;

        var limit = AppInfo.hasLowImageBandwidth ?
         24 :
         40;

        var query = {

            Limit: limit,
            Fields: "AirTime,UserData,SeriesStudio,SyncInfo",
            UserId: Dashboard.getCurrentUserId(),
            ImageTypeLimit: 1,
            EnableImageTypes: "Primary,Backdrop,Banner,Thumb"
        };

        query.ParentId = LibraryMenu.getTopParentId();

        var context = '';

        if (query.ParentId) {

            $('.scopedLibraryViewNav', page).show();
            $('.globalNav', page).hide();
            context = 'tv';

        } else {
            $('.scopedLibraryViewNav', page).hide();
            $('.globalNav', page).show();
        }

        ApiClient.getJSON(ApiClient.getUrl("Shows/Upcoming", query)).done(function (result) {

            var items = result.Items;

            if (items.length) {
                $('.noItemsMessage', page).hide();
            } else {
                $('.noItemsMessage', page).show();
            }

            $('#upcomingItems', page).html(LibraryBrowser.getPosterViewHtml({
                items: items,
                showLocationTypeIndicator: false,
                shape: "backdrop",
                showTitle: true,
                showPremiereDate: true,
                showPremiereDateIndex: true,
                preferThumb: true,
                context: context || 'home-upcoming',
                lazy: true,
                showDetailsMenu: true

            })).lazyChildren();
        });
    });


})(jQuery, document);