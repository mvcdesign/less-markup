﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

define(['app'], function (app) {
    app.provider('invokeQueue', ['$controllerProvider', '$provide', '$compileProvider', '$filterProvider', function ($controllerProvider, $provide, $compileProvider, $filterProvider) {

            this.$get = ['$templateCache', '$rootElement',
                function ($templateCache, $rootElement) {

                    var instanceInjector;

                    function getInstanceInjector() {
                        return (instanceInjector) ? instanceInjector : (instanceInjector = $rootElement.data('$injector'));
                    };

                    var providers = {
                        $controllerProvider: $controllerProvider,
                        $compileProvider: $compileProvider,
                        $filterProvider: $filterProvider,
                        $provide: $provide,
                    }

                    function runModuleInvokeQueue(module) {
                        if (!module) {
                            return;
                        }

                        if (module.hasOwnProperty("_runBlocks")) {

                            var runBlocks = module._runBlocks;
                            module._runBlocks = [];

                            angular.forEach(runBlocks, function (runBlock) {
                                getInstanceInjector().invoke(runBlock);
                            });
                        }

                        if (module.hasOwnProperty("_invokeQueue")) {
                            var invokeQueue = module._invokeQueue;
                            //module._invokeQueue = [];

                            angular.forEach(invokeQueue, function (invokeArgs) {
                                if (providers.hasOwnProperty(invokeArgs[0])) {
                                    var provider = providers[invokeArgs[0]];
                                    provider[invokeArgs[1]].apply(provider, invokeArgs[2]);
                                }
                            });
                        }
                    }

                    function fillRequires(module, requires) {

                        if (module.name == "ui.bootstrap") {
                            // it is loaded during startup
                            return;
                        }

                        if (!module.hasOwnProperty("requires")) {
                            return;
                        }

                        if (requires.indexOf(module.name) < 0) {
                            requires.push(module.name);
                        }

                        angular.forEach(module.requires, function(name) {
                            var childModule = angular.module(name);
                            if (!childModule) {
                                return;
                            }
                            fillRequires(childModule, requires);
                        });
                    }

                    return {
                        runInvokeQueue: function () {

                            var requires = [];
                            fillRequires(app, requires);

                            angular.forEach(requires, function (name) {
                                if (!name || name.trim().length == 0) {
                                    return;
                                }
                                runModuleInvokeQueue(angular.module(name));
                            });
                        }
                    }
                }
            ];
        }
    ]);
});
