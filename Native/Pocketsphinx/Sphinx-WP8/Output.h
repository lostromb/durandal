#pragma once

namespace Sphinx_WP8
{

	public ref class Output sealed
	{
	public:
		static void WriteLine(Platform::String^ message);
		static void Write(Platform::String^ message);

	private:
		Output();
	};

}