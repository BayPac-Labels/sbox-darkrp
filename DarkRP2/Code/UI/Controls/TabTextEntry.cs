using Sandbox.UI;

namespace Sandbox.UI;

[Library( "TabTextEntry" )]
public class TabTextEntry : TextEntry
{
	[Parameter]
	public Action OnTabPressed { get; set; }

	public override void OnButtonTyped( ButtonEvent e )
	{
		if ( e.Button == "tab" )
		{
			e.StopPropagation = true;
			Blur();
			OnTabPressed?.Invoke();
			return;
		}

		base.OnButtonTyped( e );
	}
}
