# Third-Party Notices

Amplify is licensed under the [MIT License](./LICENSE). It uses the third-party components listed
below, each under its own licence. This file collects the required attribution/notice text.

> Keep this file up to date: when a dependency is added, removed, or upgraded, update its entry
> here. Per the [dependency licensing policy](./docs/getting-started.md#3-nuget-packages), only
> OSI-approved permissive licences (MIT, Apache-2.0, BSD-2/3-Clause) may be added.

| Component | Licence | Project |
| --- | --- | --- |
| Microsoft.WindowsAppSDK | MIT | https://github.com/microsoft/WindowsAppSDK |
| Microsoft.Extensions.* (Hosting, Http, Logging, DependencyInjection) | MIT | https://github.com/dotnet/runtime |
| CommunityToolkit.Mvvm | MIT | https://github.com/CommunityToolkit/dotnet |
| CommunityToolkit.WinUI.Controls.SettingsControls | MIT | https://github.com/CommunityToolkit/Windows |
| H.NotifyIcon.WinUI | MIT | https://github.com/HavenDV/H.NotifyIcon |
| xunit | Apache-2.0 | https://github.com/xunit/xunit |
| xunit.runner.visualstudio | MIT | https://github.com/xunit/visualstudio.xunit |
| NSubstitute | BSD-3-Clause | https://github.com/nsubstitute/NSubstitute |
| Castle.Core (via NSubstitute) | Apache-2.0 | https://github.com/castleproject/Core |

---

## MIT-licensed components

Microsoft.WindowsAppSDK, Microsoft.Extensions.*, CommunityToolkit.Mvvm,
CommunityToolkit.WinUI.Controls.SettingsControls, H.NotifyIcon.WinUI, and
xunit.runner.visualstudio are distributed under the MIT License:

```
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

Refer to each project's repository for its exact copyright lines.

---

## Apache-2.0 components — xunit, Castle.Core

These components are licensed under the Apache License, Version 2.0. You may obtain a copy of the
licence at: http://www.apache.org/licenses/LICENSE-2.0

Apache-2.0 §4(d) only requires reproducing an upstream `NOTICE` file when one is distributed. As of
the referenced versions, neither xunit nor Castle.Core ships a `NOTICE` file — each provides only the
Apache-2.0 `LICENSE` (Castle.Core additionally restates it as `ASL - Apache Software Foundation
License.txt`) — so there is no additional NOTICE text to reproduce. Re-check on upgrade.

---

## BSD-3-Clause component — NSubstitute

```
Copyright (c) 2009 Anthony Egerton, David Tchepak and NSubstitute contributors.
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted
provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions
   and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of
   conditions and the following disclaimer in the documentation and/or other materials provided
   with the distribution.
3. Neither the name of the copyright holders nor the names of its contributors may be used to
   endorse or promote products derived from this software without specific prior written
   permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER
IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT
OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

> Verify the exact copyright line against the upstream NSubstitute `LICENSE` at release time.
