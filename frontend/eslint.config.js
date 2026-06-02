import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import pluginVue from "eslint-plugin-vue";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig([
    globalIgnores(["dist"]),
    {
        files: ["**/*.{ts,vue}"],
        extends: [js.configs.recommended, ...tseslint.configs.recommended, ...pluginVue.configs["flat/recommended"]],
        languageOptions: {
            ecmaVersion: 2020,
            globals: globals.browser,
        },
        rules: {
            "vue/multi-word-component-names": "off",
        },
    },
]);
