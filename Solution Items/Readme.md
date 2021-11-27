### ToreAurstadIT.Razor.Navigate

This VSIX Visual Studio Extension is a 64 bit extension for VS 2022 to help navigate to
parts of your MVC solution.

First you select the text of a MVC razor view to navigate to. The following is supported:

* Navigating to a partial view (Html.RenderPartial or Html.Partial)
* Navigating to Url.Content file
* Navigating to Url.Action

There are more HTML Helpers in Razor that are planned to support navigating to.

Make sure you select the html helpers listed here and also select the necessary
trailing text that identifies which partial view, or MVC controller and action
that are references (or the file of Url.Content).

After you have selected the text in the razor view to navigate to, hit the following
command combo in Visual Studio : 

```bash
Ctrl+W, Ctrl+N
```

(vieW => Next can be the 'mnemonics' here to remember - but as you see - the plugin
also supports navigating more html helpers and functionality).

This VSIX is a simple navigation tool. I have planned to extend it to more HTML 
helpers in the future. Its purpose is to help developers working with MVC to navigate
their solutions.

If you have any suggestions to improve this lib, drop me a Q&A or even a pull request at 
the repo of this extension.

Last Update 27.11.2021 
Tore Aurstad, Developer of this small extension.