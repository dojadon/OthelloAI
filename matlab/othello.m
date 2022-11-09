dotnetenv("core");
library = NET.addAssembly('C:\Users\zyand\source\repos\dojadon\OthelloAI\bin\Release\net6.0\OthelloAI.dll');

import OthelloAI.*

n = 100000;
depths = (1:3);

net_array = Tester.TestSearchedNodesCount(Program.WEIGHT, depths, n);
aa = cell2mat(cell(net_array));
a = reshape(aa, 64, [], n);
b = squeeze(mean(a, 3));

hold on
for k = 1:length(depths)
    plot(b(13:end, k));
end
hold off