﻿(function ($, document) {

    function reload(page) {

        Dashboard.showLoadingMsg();

        ApiClient.getLiveTvRecommendedPrograms({

            userId: Dashboard.getCurrentUserId(),
            IsAiring: true,
            limit: 16

        }).done(function (result) {

            var html = LibraryBrowser.getPosterViewHtml({

                items: result.Items,
                shape: "auto",
                showTitle: true,
                showParentTitle: true,
                overlayText: true,
                coverImage: true,
                lazy: true

            });

            $('.activeProgramItems', page).html(html).lazyChildren();
        });

        ApiClient.getLiveTvRecommendedPrograms({

            userId: Dashboard.getCurrentUserId(),
            IsAiring: false,
            HasAired: false,
            limit: 10,
            IsMovie: false,
            IsSports: false

        }).done(function (result) {

            var html = LibraryBrowser.getPosterViewHtml({

                items: result.Items,
                shape: "auto",
                showTitle: true,
                showParentTitle: true,
                overlayText: true,
                coverImage: true,
                lazy: true

            });

            $('.upcomingProgramItems', page).html(html).lazyChildren();
        });

        ApiClient.getLiveTvRecommendedPrograms({

            userId: Dashboard.getCurrentUserId(),
            IsAiring: false,
            HasAired: false,
            limit: 10,
            IsMovie: true

        }).done(function (result) {

            var html = LibraryBrowser.getPosterViewHtml({
                items: result.Items,
                shape: "auto",
                showTitle: true,
                coverImage: true,
                overlayText: false,
                lazy: true
            });

            $('.upcomingTvMovieItems', page).html(html).lazyChildren();
        });

        ApiClient.getLiveTvRecommendedPrograms({

            userId: Dashboard.getCurrentUserId(),
            IsAiring: false,
            HasAired: false,
            limit: 10,
            IsSports: true

        }).done(function (result) {

            var html = LibraryBrowser.getPosterViewHtml({
                items: result.Items,
                shape: "auto",
                showTitle: true,
                coverImage: true,
                overlayText: false,
                lazy: true
            });

            $('.upcomingSportsItems', page).html(html).lazyChildren();
        });
    }

    $(document).on('pagebeforeshow', "#liveTvSuggestedPage", function () {

        var page = this;

        reload(page);

    });

})(jQuery, document);