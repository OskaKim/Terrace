using System.Runtime.CompilerServices;

// テストからは内部の状態(所持金・HP など)を直接用意できるようにする
[assembly: InternalsVisibleTo("Terrace.Client.Tests.EditMode")]
[assembly: InternalsVisibleTo("Terrace.Client.Tests.PlayMode")]
